using System.Text.Json;
using Confluent.Kafka;

namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Publishes matchmaking lifecycle events to Kafka.
/// </summary>
public sealed class KafkaMatchmakingEventPublisher : IMatchmakingEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaMatchmakingEventPublisher> _logger;

    /// <summary>
    /// At this point the Kafka producer dependencies have been created and are ready for event publishing.
    /// </summary>
    public KafkaMatchmakingEventPublisher(
        IProducer<Null, string> producer,
        ILogger<KafkaMatchmakingEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// At this point a matchmaking event has been accepted by the producer service and is being sent to Kafka.
    /// </summary>
    public async Task PublishAsync(string topic, MatchmakingEvent message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message, SerializerOptions);

        var result = await _producer.ProduceAsync(
            topic,
            new Message<Null, string>
            {
                Value = payload
            },
            cancellationToken);

        _logger.LogInformation(
            "Published matchmaking event {EventType} for player {PlayerId} to {Topic} at offset {Offset}",
            message.EventType,
            message.PlayerId,
            result.Topic,
            result.Offset.Value);
    }

    /// <summary>
    /// At this point the application is shutting down and the Kafka producer is releasing resources.
    /// </summary>
    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
