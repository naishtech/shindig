namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Processes queue lifecycle events and creates basic matches when enough compatible players are available.
/// </summary>
public sealed class MatchMakerConsumerWorker
{
    private readonly IMatchmakingPoolStore _poolStore;
    private readonly IMatchResultPublisher _publisher;
    private readonly ISystemClock _clock;
    private readonly int _matchSize;

    /// <summary>
    /// At this point the worker dependencies are being prepared for queue event handling.
    /// </summary>
    public MatchMakerConsumerWorker(
        IMatchmakingPoolStore poolStore,
        IMatchResultPublisher publisher,
        KafkaWorkerOptions? options = null,
        ISystemClock? clock = null)
    {
        _poolStore = poolStore;
        _publisher = publisher;
        _clock = clock ?? new SystemClock();
        _matchSize = Math.Max(2, options?.MatchSize ?? 2);
    }

    /// <summary>
    /// At this point a queue lifecycle event is being applied to the active matchmaking pool.
    /// </summary>
    public async Task HandleAsync(MatchmakingEvent matchmakingEvent, CancellationToken cancellationToken)
    {
        switch (matchmakingEvent.EventType)
        {
            case "PLAYER_JOIN_QUEUE":
            case "PLAYER_UPDATE_ATTRIBUTES":
                var players = await _poolStore.UpsertPlayerAsync(matchmakingEvent, cancellationToken);

                if (players.Count < _matchSize)
                {
                    return;
                }

                var matchedPlayers = players
                    .Take(_matchSize)
                    .Select(player => new MatchedPlayer(player.PlayerId, player.Attributes))
                    .ToList();

                await _publisher.PublishMatchCreatedAsync(
                    new MatchCreatedEvent(
                        EventType: "MATCH_CREATED",
                        Timestamp: _clock.UtcNow,
                        MatchId: $"match-{Guid.NewGuid():N}",
                        QueueName: matchmakingEvent.QueueName,
                        Region: matchmakingEvent.Region,
                        GameMode: matchmakingEvent.GameMode,
                        SkillBracket: matchmakingEvent.SkillBracket,
                        PartitionKey: matchmakingEvent.PartitionKey,
                        Players: matchedPlayers,
                        Metadata: matchmakingEvent.Metadata),
                    cancellationToken);

                await _poolStore.RemovePlayersAsync(matchedPlayers.Select(player => player.PlayerId).ToArray(), matchmakingEvent.PartitionKey, cancellationToken);
                break;

            case "PLAYER_LEAVE_QUEUE":
            case "PLAYER_DEQUEUED":
            case "MATCH_CANCELLED":
                await _poolStore.RemovePlayerAsync(matchmakingEvent.PlayerId, matchmakingEvent.PartitionKey, cancellationToken);
                break;
        }
    }
}
