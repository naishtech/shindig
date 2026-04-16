using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;
using Moq;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

public class MatchmakingRequestHandlerTests
{
    [Fact]
    public async Task LeaveQueueAsync_DoesNotPublishLeaveEventWhenPlayerWasNotQueued()
    {
        var publisher = new Mock<IMatchmakingEventPublisher>(MockBehavior.Strict);
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();

        stateStore.Setup(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.LeaveQueueAsync(new LeaveQueueRequest("player-123", "default", "oce", "duo", "1400-1499", "player-left"), CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePlayerAsync_DoesNotPublishUpdateEventWhenPlayerWasNotQueued()
    {
        var publisher = new Mock<IMatchmakingEventPublisher>(MockBehavior.Strict);
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();

        stateStore.Setup(x => x.TryUpdatePlayerAsync(It.IsAny<UpdatePlayerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.UpdatePlayerAsync(
            new UpdatePlayerRequest(
                "player-123",
                "default",
                "oce",
                "duo",
                "1400-1499",
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelQueueAsync_DoesNotPublishDequeuedEventWhenPlayerWasNotQueued()
    {
        var publisher = new Mock<IMatchmakingEventPublisher>(MockBehavior.Strict);
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();

        stateStore.Setup(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.CancelQueueAsync(new CancelQueueRequest("player-123", "default", "oce", "duo", "1400-1499", "cancel-matchmaking"), CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueuePlayerAsync_DoesNotPublishJoinEventWhenPlayerIsAlreadyQueued()
    {
        var publisher = new Mock<IMatchmakingEventPublisher>(MockBehavior.Strict);
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();

        stateStore.Setup(x => x.TryQueuePlayerAsync(It.IsAny<QueuePlayerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.QueuePlayerAsync(
            new QueuePlayerRequest(
                "player-123",
                "default",
                "oce",
                "duo",
                "1400-1499",
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueuePlayerAsync_PublishesJoinEventWithCompositePartitionKey()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 0, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();
        MatchmakingEvent? publishedEvent = null;
        string? publishedTopic = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        stateStore.Setup(x => x.TryQueuePlayerAsync(It.IsAny<QueuePlayerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((topic, message, _) =>
            {
                publishedTopic = topic;
                publishedEvent = message;
            })
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        var request = new QueuePlayerRequest(
            PlayerId: "player-123",
            QueueName: "default",
            Region: "oce",
            GameMode: "duo",
            SkillBracket: "1400-1499",
            Attributes: new Dictionary<string, string> { ["latency"] = "32" },
            Metadata: new Dictionary<string, string> { ["gameId"] = "game-xyz" });

        await sut.QueuePlayerAsync(request, CancellationToken.None);

        stateStore.Verify(x => x.TryQueuePlayerAsync(It.IsAny<QueuePlayerRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("mm.player.queue", publishedTopic);
        Assert.NotNull(publishedEvent);
        Assert.Equal("PLAYER_JOIN_QUEUE", publishedEvent!.EventType);
        Assert.Equal("player-123", publishedEvent.PlayerId);
        Assert.Equal("default", publishedEvent.QueueName);
        Assert.Equal("oce:duo:1400-1499", publishedEvent.PartitionKey);
        Assert.Equal(utcNow, publishedEvent.Timestamp);
        Assert.Equal("32", publishedEvent.Attributes["latency"]);
        Assert.Equal("game-xyz", publishedEvent.Metadata["gameId"]);
    }

    [Fact]
    public async Task LeaveQueueAsync_PublishesLeaveQueueEvent()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 3, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();
        MatchmakingEvent? publishedEvent = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        stateStore.Setup(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((_, message, _) => publishedEvent = message)
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.LeaveQueueAsync(new LeaveQueueRequest("player-123", "default", "oce", "duo", "1400-1499", "player-left"), CancellationToken.None);

        stateStore.Verify(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(publishedEvent);
        Assert.Equal("PLAYER_LEAVE_QUEUE", publishedEvent!.EventType);
        Assert.Equal("player-left", publishedEvent.Reason);
        Assert.Equal("oce:duo:1400-1499", publishedEvent.PartitionKey);
        Assert.Equal(utcNow, publishedEvent.Timestamp);
    }

    [Fact]
    public async Task UpdatePlayerAsync_PublishesUpdateAttributesEvent()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 4, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();
        MatchmakingEvent? publishedEvent = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        stateStore.Setup(x => x.TryUpdatePlayerAsync(It.IsAny<UpdatePlayerRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((_, message, _) => publishedEvent = message)
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.UpdatePlayerAsync(
            new UpdatePlayerRequest(
                "player-123",
                "default",
                "oce",
                "duo",
                "1400-1499",
                new Dictionary<string, string> { ["latency"] = "28", ["mmr"] = "1450" },
                new Dictionary<string, string> { ["gameId"] = "game-xyz" }),
            CancellationToken.None);

        stateStore.Verify(x => x.TryUpdatePlayerAsync(It.IsAny<UpdatePlayerRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(publishedEvent);
        Assert.Equal("PLAYER_UPDATE_ATTRIBUTES", publishedEvent!.EventType);
        Assert.Equal("28", publishedEvent.Attributes["latency"]);
        Assert.Equal("1450", publishedEvent.Attributes["mmr"]);
        Assert.Equal("game-xyz", publishedEvent.Metadata["gameId"]);
        Assert.Equal("oce:duo:1400-1499", publishedEvent.PartitionKey);
        Assert.Equal(utcNow, publishedEvent.Timestamp);
    }

    [Fact]
    public async Task CancelQueueAsync_PublishesPlayerDequeuedEvent()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 5, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        var stateStore = new Mock<IPlayerQueueStateStore>();
        MatchmakingEvent? publishedEvent = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        stateStore.Setup(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((_, message, _) => publishedEvent = message)
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, stateStore.Object, "mm.player.queue");

        await sut.CancelQueueAsync(new CancelQueueRequest("player-123", "default", "oce", "duo", "1400-1499", "cancel-matchmaking"), CancellationToken.None);

        stateStore.Verify(x => x.TryRemovePlayerAsync("player-123", It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(publishedEvent);
        Assert.Equal("PLAYER_DEQUEUED", publishedEvent!.EventType);
        Assert.Equal("cancel-matchmaking", publishedEvent.Reason);
        Assert.Equal("oce:duo:1400-1499", publishedEvent.PartitionKey);
        Assert.Equal(utcNow, publishedEvent.Timestamp);
    }
}
