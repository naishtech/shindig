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
}
