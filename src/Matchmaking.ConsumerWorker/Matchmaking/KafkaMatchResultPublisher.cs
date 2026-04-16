namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Publishes created match events to Kafka for downstream consumers.
/// </summary>
public sealed class KafkaMatchResultPublisher : IMatchResultPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaWorkerOptions _options;
    private readonly ILogger<KafkaMatchResultPublisher> _logger;

    /// <summary>
    /// At this point the Kafka match result publisher is being prepared for output event delivery.
    /// </summary>
    public KafkaMatchResultPublisher(
        IProducer<Null, string> producer,
        KafkaWorkerOptions options,
        ILogger<KafkaMatchResultPublisher> logger)
    {
        _producer = producer;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// At this point a created match is being serialized and published to Kafka.
    /// </summary>
    public async Task PublishMatchCreatedAsync(MatchCreatedEvent matchCreatedEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(matchCreatedEvent);

        var result = await _producer.ProduceAsync(
            _options.OutputTopicName,
            new Message<Null, string> { Value = payload },
            cancellationToken);

        _logger.LogInformation("Published match {MatchId} to {TopicName} at offset {Offset}.", matchCreatedEvent.MatchId, _options.OutputTopicName, result.Offset);
    }
}
