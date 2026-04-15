namespace Matchmaking.Infrastructure.Ping;

/// <summary>
/// Publishes infrastructure ping messages to the configured transport.
/// </summary>
public interface IPingPublisher
{
    /// <summary>
    /// At this point the ping event has been created and is ready to be sent to the queue topic.
    /// </summary>
    Task PublishAsync(string topic, InfraPingMessage message, CancellationToken cancellationToken);
}
