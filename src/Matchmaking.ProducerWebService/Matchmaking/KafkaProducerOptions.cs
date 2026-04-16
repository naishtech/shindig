namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Stores the Kafka connection settings used by the producer web service.
/// </summary>
public sealed class KafkaProducerOptions
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers used by the producer service.
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Gets or sets the Kafka client identifier used by the producer service.
    /// </summary>
    public string ClientId { get; set; } = "matchmaker-producer-web";

    /// <summary>
    /// Gets or sets the Kafka topic used for queue lifecycle events.
    /// </summary>
    public string TopicName { get; set; } = "mm.player.queue";
}
