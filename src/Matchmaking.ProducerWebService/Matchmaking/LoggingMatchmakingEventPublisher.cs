using System.Text.Json;

namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Logs matchmaking events until a Kafka-backed publisher is wired in.
/// </summary>
public sealed class LoggingMatchmakingEventPublisher : IMatchmakingEventPublisher
{
    private readonly ILogger<LoggingMatchmakingEventPublisher> _logger;

    /// <summary>
    /// At this point the publisher is ready to forward matchmaking events to the configured sink.
    /// </summary>
    public LoggingMatchmakingEventPublisher(ILogger<LoggingMatchmakingEventPublisher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// At this point a matchmaking event has been accepted by the producer service and is being emitted.
    /// </summary>
    public Task PublishAsync(string topic, MatchmakingEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Publishing matchmaking event to {Topic}: {Payload}",
            topic,
            JsonSerializer.Serialize(message));

        return Task.CompletedTask;
    }
}
