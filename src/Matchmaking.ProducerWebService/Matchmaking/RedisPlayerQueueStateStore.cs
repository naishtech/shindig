using System.Text.Json;
using StackExchange.Redis;

namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Uses Redis to coordinate queue state and suppress duplicate concurrent joins.
/// </summary>
public sealed class RedisPlayerQueueStateStore : IPlayerQueueStateStore
{
    private static readonly TimeSpan StateExpiry = TimeSpan.FromHours(2);
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(5);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisPlayerQueueStateStore> _logger;

    /// <summary>
    /// At this point the Redis-backed state store is being prepared for matchmaking request coordination.
    /// </summary>
    public RedisPlayerQueueStateStore(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisPlayerQueueStateStore> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

    /// <summary>
    /// At this point a join request is attempting to claim the player's queued state without duplication.
    /// </summary>
    public async Task<bool> TryQueuePlayerAsync(QueuePlayerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var lockToken = Guid.NewGuid().ToString("N");
        var lockKey = BuildLockKey(request.PlayerId);

        var lockTaken = await database.LockTakeAsync(lockKey, lockToken, LockExpiry);

        if (!lockTaken)
        {
            _logger.LogInformation("Skipped duplicate join handling for player {PlayerId} because another request is already in progress.", request.PlayerId);
            return false;
        }

        try
        {
            var storedState = JsonSerializer.Serialize(new
            {
                request.PlayerId,
                request.QueueName,
                request.Region,
                request.GameMode,
                request.SkillBracket,
                request.Attributes,
                request.Metadata,
                PartitionKey = $"{request.Region}:{request.GameMode}:{request.SkillBracket}",
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return await database.StringSetAsync(BuildPlayerKey(request.PlayerId), storedState, StateExpiry, When.NotExists);
        }
        finally
        {
            await database.LockReleaseAsync(lockKey, lockToken);
        }
    }

    /// <summary>
    /// At this point the queued players for a named queue are being loaded from Redis state.
    /// </summary>
    public async Task<IReadOnlyList<QueuePlayerRequest>> GetQueuedPlayersAsync(string queueName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var players = new List<QueuePlayerRequest>();
        var observedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in _connectionMultiplexer.GetEndPoints())
        {
            var server = _connectionMultiplexer.GetServer(endpoint);

            if (!server.IsConnected)
            {
                continue;
            }

            foreach (var key in server.Keys(database.Database, pattern: "mm:player:*"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var keyName = key.ToString();

                if (!observedKeys.Add(keyName))
                {
                    continue;
                }

                var storedState = await database.StringGetAsync(key);

                if (!storedState.HasValue ||
                    !TryReadQueuedPlayer(storedState!, out var player) ||
                    !string.Equals(player.QueueName, queueName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                players.Add(player);
            }
        }

        return players;
    }

    /// <summary>
    /// At this point a queued player's state is being updated while preserving the existing queue claim.
    /// </summary>
    public async Task<bool> TryUpdatePlayerAsync(UpdatePlayerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var lockToken = Guid.NewGuid().ToString("N");
        var lockKey = BuildLockKey(request.PlayerId);

        var lockTaken = await database.LockTakeAsync(lockKey, lockToken, LockExpiry);

        if (!lockTaken)
        {
            _logger.LogInformation("Skipped queue state update for player {PlayerId} because another request is already in progress.", request.PlayerId);
            return false;
        }

        try
        {
            var storedState = JsonSerializer.Serialize(new
            {
                request.PlayerId,
                request.QueueName,
                request.Region,
                request.GameMode,
                request.SkillBracket,
                request.Attributes,
                request.Metadata,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            return await database.StringSetAsync(BuildPlayerKey(request.PlayerId), storedState, StateExpiry, When.Exists);
        }
        finally
        {
            await database.LockReleaseAsync(lockKey, lockToken);
        }
    }

    /// <summary>
    /// At this point the player's stored queue claim is being removed after leave or cancel handling.
    /// </summary>
    public async Task<bool> TryRemovePlayerAsync(string playerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var lockToken = Guid.NewGuid().ToString("N");
        var lockKey = BuildLockKey(playerId);

        var lockTaken = await database.LockTakeAsync(lockKey, lockToken, LockExpiry);

        if (!lockTaken)
        {
            _logger.LogInformation("Skipped queue state removal for player {PlayerId} because another request is already in progress.", playerId);
            return false;
        }

        try
        {
            return await database.KeyDeleteAsync(BuildPlayerKey(playerId));
        }
        finally
        {
            await database.LockReleaseAsync(lockKey, lockToken);
        }
    }

    private static string BuildPlayerKey(string playerId)
    {
        return $"mm:player:{playerId}";
    }

    private static string BuildLockKey(string playerId)
    {
        return $"mm:lock:{playerId}";
    }

    private static bool TryReadQueuedPlayer(string storedState, out QueuePlayerRequest player)
    {
        try
        {
            using var document = JsonDocument.Parse(storedState);
            var root = document.RootElement;

            player = new QueuePlayerRequest(
                PlayerId: ReadRequiredString(root, nameof(QueuePlayerRequest.PlayerId)),
                QueueName: ReadRequiredString(root, nameof(QueuePlayerRequest.QueueName)),
                Region: ReadRequiredString(root, nameof(QueuePlayerRequest.Region)),
                GameMode: ReadRequiredString(root, nameof(QueuePlayerRequest.GameMode)),
                SkillBracket: ReadRequiredString(root, nameof(QueuePlayerRequest.SkillBracket)),
                Attributes: ReadDictionary(root, nameof(QueuePlayerRequest.Attributes)),
                Metadata: ReadDictionary(root, nameof(QueuePlayerRequest.Metadata)));

            return true;
        }
        catch (JsonException)
        {
            player = new QueuePlayerRequest(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null);
            return false;
        }
        catch (InvalidOperationException)
        {
            player = new QueuePlayerRequest(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null);
            return false;
        }
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyDictionary<string, string>? ReadDictionary(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in property.EnumerateObject())
        {
            values[item.Name] = item.Value.GetString() ?? string.Empty;
        }

        return values;
    }
}
