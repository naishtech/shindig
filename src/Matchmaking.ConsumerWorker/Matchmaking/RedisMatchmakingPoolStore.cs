namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Uses Redis to maintain the active matchmaking pool for each partition.
/// </summary>
public sealed class RedisMatchmakingPoolStore : IMatchmakingPoolStore
{
    private static readonly TimeSpan StateExpiry = TimeSpan.FromHours(2);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    /// <summary>
    /// At this point the worker is preparing to persist and query queue state in Redis.
    /// </summary>
    public RedisMatchmakingPoolStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    /// <summary>
    /// At this point a player is being added or refreshed in a Redis-backed partition pool.
    /// </summary>
    public async Task<IReadOnlyCollection<PlayerPoolEntry>> UpsertPlayerAsync(MatchmakingEvent matchmakingEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var playerKey = BuildPlayerKey(matchmakingEvent.PlayerId);
        var poolKey = BuildPoolKey(matchmakingEvent.PartitionKey);

        var player = new PlayerPoolEntry(
            PlayerId: matchmakingEvent.PlayerId,
            QueueName: matchmakingEvent.QueueName,
            Region: matchmakingEvent.Region,
            GameMode: matchmakingEvent.GameMode,
            SkillBracket: matchmakingEvent.SkillBracket,
            PartitionKey: matchmakingEvent.PartitionKey,
            Attributes: matchmakingEvent.Attributes,
            Metadata: matchmakingEvent.Metadata,
            JoinedAt: matchmakingEvent.Timestamp,
            UpdatedAt: matchmakingEvent.Timestamp);

        await database.StringSetAsync(playerKey, JsonSerializer.Serialize(player), StateExpiry);
        await database.SortedSetAddAsync(poolKey, matchmakingEvent.PlayerId, matchmakingEvent.Timestamp.UtcTicks);
        await database.KeyExpireAsync(poolKey, StateExpiry);

        var players = new List<PlayerPoolEntry>();
        var ids = await database.SortedSetRangeByRankAsync(poolKey, 0, 9);

        foreach (var id in ids)
        {
            var stored = await database.StringGetAsync(BuildPlayerKey(id!));

            if (stored.IsNullOrEmpty)
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<PlayerPoolEntry>(stored.ToString());

            if (entry is not null)
            {
                players.Add(entry);
            }
        }

        return players;
    }

    /// <summary>
    /// At this point a player is being removed from the Redis-backed pool.
    /// </summary>
    public async Task RemovePlayerAsync(string playerId, string partitionKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(BuildPlayerKey(playerId));
        await database.SortedSetRemoveAsync(BuildPoolKey(partitionKey), playerId);
    }

    /// <summary>
    /// At this point matched players are being removed from the Redis-backed pool after match creation.
    /// </summary>
    public async Task RemovePlayersAsync(IReadOnlyCollection<string> playerIds, string partitionKey, CancellationToken cancellationToken)
    {
        foreach (var playerId in playerIds)
        {
            await RemovePlayerAsync(playerId, partitionKey, cancellationToken);
        }
    }

    private static string BuildPlayerKey(string playerId)
    {
        return $"mm:worker:player:{playerId}";
    }

    private static string BuildPoolKey(string partitionKey)
    {
        return $"mm:worker:pool:{partitionKey}";
    }
}
