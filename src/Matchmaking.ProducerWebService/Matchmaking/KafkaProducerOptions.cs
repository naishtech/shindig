namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Stores the Kafka connection settings used by the producer web service.
/// </summary>
public sealed class KafkaProducerOptions
{
    /// <summary>
    /// At this point the producer is preparing to connect to the configured Kafka bootstrap servers.
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// At this point the producer identity is being attached to outgoing Kafka requests.
    /// </summary>
    public string ClientId { get; set; } = "matchmaker-producer-web";

    /// <summary>
    /// At this point the producer is selecting the Kafka topic used for queue lifecycle events.
    /// </summary>
    public string TopicName { get; set; } = "mm.player.queue";
}
