namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Publishes matchmaking events to the configured transport.
/// </summary>
public interface IMatchmakingEventPublisher
{
    /// <summary>
    /// At this point a matchmaking event has been prepared and is ready to be sent to the queue topic.
    /// </summary>
    Task PublishAsync(string topic, MatchmakingEvent message, CancellationToken cancellationToken);
}
