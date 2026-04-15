namespace Matchmaking.Infrastructure.Ping;

/// <summary>
/// Represents the infrastructure ping event sent to Kafka.
/// </summary>
public sealed record InfraPingMessage(
    string EventType,
    DateTimeOffset Timestamp,
    string Source,
    string Environment,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Describes the outcome of a ping publish attempt.
/// </summary>
public sealed record PingPublishResult(bool Success, string Topic);

/// <summary>
/// Builds and publishes infrastructure heartbeat messages.
/// </summary>
public sealed class KafkaPingFunction
{
    private readonly IPingPublisher _publisher;
    private readonly ISystemClock _clock;
    private readonly string _environmentName;
    private readonly string _topicName;

    /// <summary>
    /// At this point the function dependencies and target topic are being prepared for ping handling.
    /// </summary>
    public KafkaPingFunction(
        IPingPublisher publisher,
        ISystemClock clock,
        string environmentName,
        string topicName)
    {
        _publisher = publisher;
        _clock = clock;
        _environmentName = environmentName;
        _topicName = topicName;
    }

    /// <summary>
    /// At this point we have received a ping request and are ready to publish the heartbeat message.
    /// </summary>
    public async Task<PingPublishResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var message = new InfraPingMessage(
            EventType: "INFRA_PING",
            Timestamp: _clock.UtcNow,
            Source: "lambda-kafka-ping",
            Environment: _environmentName,
            CorrelationId: Guid.NewGuid().ToString("N"),
            Metadata: new Dictionary<string, string>
            {
                ["service"] = "generic-matchmaking-infra",
                ["purpose"] = "connectivity-check"
            });

        await _publisher.PublishAsync(_topicName, message, cancellationToken);

        return new PingPublishResult(true, _topicName);
    }
}
