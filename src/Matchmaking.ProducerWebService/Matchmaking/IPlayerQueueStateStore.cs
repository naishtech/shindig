namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Stores player queue state so concurrent requests can be coordinated safely.
/// </summary>
public interface IPlayerQueueStateStore
{
    /// <summary>
    /// At this point a join request is being evaluated to determine whether the player is already queued.
    /// </summary>
    Task<bool> TryQueuePlayerAsync(QueuePlayerRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// At this point a queued player's stored state is being refreshed after an attribute change.
    /// </summary>
    Task<bool> TryUpdatePlayerAsync(UpdatePlayerRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// At this point a player is leaving the queue and any stored state should be removed.
    /// </summary>
    Task<bool> TryRemovePlayerAsync(string playerId, CancellationToken cancellationToken);
}
