namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Continuously consumes queue lifecycle events from Kafka and passes them to the matchmaking worker.
/// </summary>
public sealed class KafkaQueueConsumerBackgroundService : BackgroundService
{
    private readonly KafkaWorkerOptions _options;
    private readonly MatchMakerConsumerWorker _worker;
    private readonly ILogger<KafkaQueueConsumerBackgroundService> _logger;

    /// <summary>
    /// At this point the background consumer is being prepared for Kafka polling.
    /// </summary>
    public KafkaQueueConsumerBackgroundService(
        KafkaWorkerOptions options,
        MatchMakerConsumerWorker worker,
        ILogger<KafkaQueueConsumerBackgroundService> logger)
    {
        _options = options;
        _worker = worker;
        _logger = logger;
    }

    /// <summary>
    /// At this point the service is polling Kafka for queue lifecycle events.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        }).Build();

        consumer.Subscribe(_options.InputTopicName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));

                if (result?.Message?.Value is null)
                {
                    continue;
                }

                var matchmakingEvent = JsonSerializer.Deserialize<MatchmakingEvent>(result.Message.Value);

                if (matchmakingEvent is null)
                {
                    continue;
                }

                await _worker.HandleAsync(matchmakingEvent, stoppingToken);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Kafka consume failed for matchmaking worker.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Received invalid matchmaking event payload.");
            }
        }
    }
}
