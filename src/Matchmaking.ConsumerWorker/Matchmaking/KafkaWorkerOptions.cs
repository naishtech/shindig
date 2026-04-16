namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Stores the Kafka settings used by the consumer worker.
/// </summary>
public sealed class KafkaWorkerOptions
{
    /// <summary>
    /// At this point the worker is preparing to connect to the Kafka bootstrap servers.
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// At this point the worker is preparing to subscribe to the queue lifecycle topic.
    /// </summary>
    public string InputTopicName { get; set; } = "mm.player.queue";

    /// <summary>
    /// At this point the worker is preparing to publish created matches to Kafka.
    /// </summary>
    public string OutputTopicName { get; set; } = "mm.match.created";

    /// <summary>
    /// At this point the worker is determining how many compatible players are required to form a match.
    /// </summary>
    public int MatchSize { get; set; } = 2;

    /// <summary>
    /// At this point the worker is preparing its Kafka consumer group identity.
    /// </summary>
    public string GroupId { get; set; } = "matchmaker-consumer-worker";
}
