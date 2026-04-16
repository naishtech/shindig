namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Publishes matchmaking results for downstream consumers.
/// </summary>
public interface IMatchResultPublisher
{
    /// <summary>
    /// At this point a match has been formed and is ready to be published.
    /// </summary>
    Task PublishMatchCreatedAsync(MatchCreatedEvent matchCreatedEvent, CancellationToken cancellationToken);
}
