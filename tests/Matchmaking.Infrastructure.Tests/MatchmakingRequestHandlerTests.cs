using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;
using Moq;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

public class MatchmakingRequestHandlerTests
{
    [Fact]
    public async Task QueuePlayerAsync_PublishesJoinEventWithCompositePartitionKey()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 0, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        MatchmakingEvent? publishedEvent = null;
        string? publishedTopic = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((topic, message, _) =>
            {
                publishedTopic = topic;
                publishedEvent = message;
            })
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, "mm.player.queue");

        var request = new QueuePlayerRequest(
            PlayerId: "player-123",
            QueueName: "default",
            Region: "oce",
            GameMode: "duo",
            SkillBracket: "1400-1499",
            Attributes: new Dictionary<string, string> { ["latency"] = "32" },
            Metadata: new Dictionary<string, string> { ["gameId"] = "game-xyz" });

        await sut.QueuePlayerAsync(request, CancellationToken.None);

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
    public async Task CancelQueueAsync_PublishesPlayerDequeuedEvent()
    {
        var utcNow = new DateTimeOffset(2026, 4, 16, 10, 5, 0, TimeSpan.Zero);
        var publisher = new Mock<IMatchmakingEventPublisher>();
        var clock = new Mock<ISystemClock>();
        MatchmakingEvent? publishedEvent = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<MatchmakingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<string, MatchmakingEvent, CancellationToken>((_, message, _) => publishedEvent = message)
            .Returns(Task.CompletedTask);

        var sut = new MatchmakingRequestHandler(publisher.Object, clock.Object, "mm.player.queue");

        await sut.CancelQueueAsync(new CancelQueueRequest("player-123", "default", "oce", "duo", "1400-1499", "cancel-matchmaking"), CancellationToken.None);

        Assert.NotNull(publishedEvent);
        Assert.Equal("PLAYER_DEQUEUED", publishedEvent!.EventType);
        Assert.Equal("cancel-matchmaking", publishedEvent.Reason);
        Assert.Equal("oce:duo:1400-1499", publishedEvent.PartitionKey);
        Assert.Equal(utcNow, publishedEvent.Timestamp);
    }
}
