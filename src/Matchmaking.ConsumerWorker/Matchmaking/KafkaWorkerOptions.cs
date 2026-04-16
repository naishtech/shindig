namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Stores the Kafka settings used by the consumer worker.
/// </summary>
public sealed class KafkaWorkerOptions
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers used by the consumer worker.
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Gets or sets the Kafka topic that receives player queue lifecycle events.
    /// </summary>
    public string InputTopicName { get; set; } = "mm.player.queue";

    /// <summary>
    /// Gets or sets the Kafka topic that receives created match events.
    /// </summary>
    public string OutputTopicName { get; set; } = "mm.match.created";

    /// <summary>
    /// Gets or sets the number of compatible players required to form a match.
    /// </summary>
    public int MatchSize { get; set; } = 2;

    /// <summary>
    /// Gets or sets the Kafka consumer group identifier for the worker.
    /// </summary>
    public string GroupId { get; set; } = "matchmaker-consumer-worker";
}
