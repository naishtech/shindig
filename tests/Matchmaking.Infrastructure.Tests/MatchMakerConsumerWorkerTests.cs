using Matchmaking.ConsumerWorker.Matchmaking;
using Matchmaking.ProducerWebService.Matchmaking;
using Moq;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

public sealed class MatchMakerConsumerWorkerTests
{
    [Fact]
    public async Task HandleAsync_ForSingleJoin_UpdatesPoolWithoutPublishingMatch()
    {
        var poolStore = new Mock<IMatchmakingPoolStore>(MockBehavior.Strict);
        var publisher = new Mock<IMatchResultPublisher>(MockBehavior.Strict);

        var matchmakingEvent = CreateJoinEvent("player-1");

        poolStore
            .Setup(x => x.UpsertPlayerAsync(matchmakingEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePoolEntry("player-1")
            });

        var sut = new MatchMakerConsumerWorker(poolStore.Object, publisher.Object);

        await sut.HandleAsync(matchmakingEvent, CancellationToken.None);

        poolStore.Verify(x => x.UpsertPlayerAsync(matchmakingEvent, It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.PublishMatchCreatedAsync(It.IsAny<MatchCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForSecondCompatibleJoin_PublishesMatchAndRemovesMatchedPlayers()
    {
        var poolStore = new Mock<IMatchmakingPoolStore>(MockBehavior.Strict);
        var publisher = new Mock<IMatchResultPublisher>();
        MatchCreatedEvent? publishedEvent = null;

        var matchmakingEvent = CreateJoinEvent("player-2");

        poolStore
            .Setup(x => x.UpsertPlayerAsync(matchmakingEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePoolEntry("player-1"),
                CreatePoolEntry("player-2")
            });

        poolStore
            .Setup(x => x.RemovePlayersAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 2 && ids.Contains("player-1") && ids.Contains("player-2")),
                matchmakingEvent.PartitionKey,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(x => x.PublishMatchCreatedAsync(It.IsAny<MatchCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<MatchCreatedEvent, CancellationToken>((matchEvent, _) => publishedEvent = matchEvent)
            .Returns(Task.CompletedTask);

        var sut = new MatchMakerConsumerWorker(poolStore.Object, publisher.Object);

        await sut.HandleAsync(matchmakingEvent, CancellationToken.None);

        Assert.NotNull(publishedEvent);
        Assert.Equal("MATCH_CREATED", publishedEvent!.EventType);
        Assert.Equal("default", publishedEvent.QueueName);
        Assert.Equal(2, publishedEvent.Players.Count);
        Assert.Contains(publishedEvent.Players, player => player.PlayerId == "player-1");
        Assert.Contains(publishedEvent.Players, player => player.PlayerId == "player-2");
    }

    [Fact]
    public async Task HandleAsync_ForDequeuedEvent_RemovesPlayerWithoutPublishingMatch()
    {
        var poolStore = new Mock<IMatchmakingPoolStore>(MockBehavior.Strict);
        var publisher = new Mock<IMatchResultPublisher>(MockBehavior.Strict);

        var matchmakingEvent = CreateCancelEvent("player-1");

        poolStore
            .Setup(x => x.RemovePlayerAsync(matchmakingEvent.PlayerId, matchmakingEvent.PartitionKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new MatchMakerConsumerWorker(poolStore.Object, publisher.Object);

        await sut.HandleAsync(matchmakingEvent, CancellationToken.None);

        poolStore.Verify(x => x.RemovePlayerAsync(matchmakingEvent.PlayerId, matchmakingEvent.PartitionKey, It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.PublishMatchCreatedAsync(It.IsAny<MatchCreatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static MatchmakingEvent CreateJoinEvent(string playerId)
    {
        return new MatchmakingEvent(
            EventType: "PLAYER_JOIN_QUEUE",
            Timestamp: new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
            PlayerId: playerId,
            QueueName: "default",
            Region: "oce",
            GameMode: "duo",
            SkillBracket: "1400-1499",
            PartitionKey: "oce:duo:1400-1499",
            Attributes: new Dictionary<string, string> { ["latency"] = "32", ["mmr"] = "1450" },
            Metadata: new Dictionary<string, string> { ["gameId"] = "game-xyz" });
    }

    private static MatchmakingEvent CreateCancelEvent(string playerId)
    {
        return new MatchmakingEvent(
            EventType: "PLAYER_DEQUEUED",
            Timestamp: new DateTimeOffset(2026, 4, 16, 12, 1, 0, TimeSpan.Zero),
            PlayerId: playerId,
            QueueName: "default",
            Region: "oce",
            GameMode: "duo",
            SkillBracket: "1400-1499",
            PartitionKey: "oce:duo:1400-1499",
            Attributes: new Dictionary<string, string>(),
            Metadata: new Dictionary<string, string>(),
            Reason: "cancel-matchmaking");
    }

    private static PlayerPoolEntry CreatePoolEntry(string playerId)
    {
        return new PlayerPoolEntry(
            PlayerId: playerId,
            QueueName: "default",
            Region: "oce",
            GameMode: "duo",
            SkillBracket: "1400-1499",
            PartitionKey: "oce:duo:1400-1499",
            Attributes: new Dictionary<string, string> { ["latency"] = "32", ["mmr"] = "1450" },
            Metadata: new Dictionary<string, string> { ["gameId"] = "game-xyz" },
            JoinedAt: new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero));
    }
}
