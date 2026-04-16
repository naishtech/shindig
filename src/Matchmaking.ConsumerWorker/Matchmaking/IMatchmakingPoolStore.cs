namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Stores the worker's active matchmaking pool for queue coordination.
/// </summary>
public interface IMatchmakingPoolStore
{
    /// <summary>
    /// At this point a queue event is updating the active player pool for a partition.
    /// </summary>
    Task<IReadOnlyCollection<PlayerPoolEntry>> UpsertPlayerAsync(MatchmakingEvent matchmakingEvent, CancellationToken cancellationToken);

    /// <summary>
    /// At this point a player is being removed from the active pool because they left or cancelled.
    /// </summary>
    Task RemovePlayerAsync(string playerId, string partitionKey, CancellationToken cancellationToken);

    /// <summary>
    /// At this point matched players are being removed from the active pool after a match is created.
    /// </summary>
    Task RemovePlayersAsync(IReadOnlyCollection<string> playerIds, string partitionKey, CancellationToken cancellationToken);
}
