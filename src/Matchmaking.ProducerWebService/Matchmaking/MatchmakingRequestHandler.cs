using Matchmaking.Infrastructure.Ping;

namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Builds matchmaking lifecycle events from incoming API requests.
/// </summary>
public sealed class MatchmakingRequestHandler
{
    private readonly IMatchmakingEventPublisher _publisher;
    private readonly ISystemClock _clock;
    private readonly string _topicName;

    /// <summary>
    /// At this point the request handler dependencies and target topic are being prepared for queue event publishing.
    /// </summary>
    public MatchmakingRequestHandler(
        IMatchmakingEventPublisher publisher,
        ISystemClock clock,
        string topicName)
    {
        _publisher = publisher;
        _clock = clock;
        _topicName = topicName;
    }

    /// <summary>
    /// At this point a player has requested to join matchmaking and a queue event is ready to publish.
    /// </summary>
    public Task QueuePlayerAsync(QueuePlayerRequest request, CancellationToken cancellationToken)
    {
        var matchmakingEvent = CreateEvent(
            eventType: "PLAYER_JOIN_QUEUE",
            playerId: request.PlayerId,
            queueName: request.QueueName,
            region: request.Region,
            gameMode: request.GameMode,
            skillBracket: request.SkillBracket,
            attributes: request.Attributes,
            metadata: request.Metadata,
            reason: null);

        return _publisher.PublishAsync(_topicName, matchmakingEvent, cancellationToken);
    }

    /// <summary>
    /// At this point a player has requested to leave matchmaking and a leave event is ready to publish.
    /// </summary>
    public Task LeaveQueueAsync(LeaveQueueRequest request, CancellationToken cancellationToken)
    {
        var matchmakingEvent = CreateEvent(
            eventType: "PLAYER_LEAVE_QUEUE",
            playerId: request.PlayerId,
            queueName: request.QueueName,
            region: request.Region,
            gameMode: request.GameMode,
            skillBracket: request.SkillBracket,
            attributes: null,
            metadata: null,
            reason: request.Reason);

        return _publisher.PublishAsync(_topicName, matchmakingEvent, cancellationToken);
    }

    /// <summary>
    /// At this point a queued player's attributes have changed and an update event is ready to publish.
    /// </summary>
    public Task UpdatePlayerAsync(UpdatePlayerRequest request, CancellationToken cancellationToken)
    {
        var matchmakingEvent = CreateEvent(
            eventType: "PLAYER_UPDATE_ATTRIBUTES",
            playerId: request.PlayerId,
            queueName: request.QueueName,
            region: request.Region,
            gameMode: request.GameMode,
            skillBracket: request.SkillBracket,
            attributes: request.Attributes,
            metadata: request.Metadata,
            reason: null);

        return _publisher.PublishAsync(_topicName, matchmakingEvent, cancellationToken);
    }

    /// <summary>
    /// At this point a player has requested to cancel matchmaking and a dequeue event is ready to publish.
    /// </summary>
    public Task CancelQueueAsync(CancelQueueRequest request, CancellationToken cancellationToken)
    {
        var matchmakingEvent = CreateEvent(
            eventType: "PLAYER_DEQUEUED",
            playerId: request.PlayerId,
            queueName: request.QueueName,
            region: request.Region,
            gameMode: request.GameMode,
            skillBracket: request.SkillBracket,
            attributes: null,
            metadata: null,
            reason: request.Reason);

        return _publisher.PublishAsync(_topicName, matchmakingEvent, cancellationToken);
    }

    private MatchmakingEvent CreateEvent(
        string eventType,
        string playerId,
        string queueName,
        string region,
        string gameMode,
        string skillBracket,
        IReadOnlyDictionary<string, string>? attributes,
        IReadOnlyDictionary<string, string>? metadata,
        string? reason)
    {
        return new MatchmakingEvent(
            EventType: eventType,
            Timestamp: _clock.UtcNow,
            PlayerId: playerId,
            QueueName: queueName,
            Region: region,
            GameMode: gameMode,
            SkillBracket: skillBracket,
            PartitionKey: BuildPartitionKey(region, gameMode, skillBracket),
            Attributes: attributes ?? new Dictionary<string, string>(),
            Metadata: metadata ?? new Dictionary<string, string>(),
            Reason: reason);
    }

    private static string BuildPartitionKey(string region, string gameMode, string skillBracket)
    {
        return $"{region}:{gameMode}:{skillBracket}";
    }
}
