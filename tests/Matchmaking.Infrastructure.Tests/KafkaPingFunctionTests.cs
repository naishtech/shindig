using Matchmaking.Infrastructure.Ping;
using Moq;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

public class KafkaPingFunctionTests
{
    [Fact]
    public async Task HandleAsync_PublishesInfrastructurePingEventToConfiguredTopic()
    {
        var utcNow = new DateTimeOffset(2026, 4, 15, 5, 0, 0, TimeSpan.Zero);
        var publisher = new Mock<IPingPublisher>();
        var clock = new Mock<ISystemClock>();
        InfraPingMessage? publishedMessage = null;
        string? publishedTopic = null;

        clock.SetupGet(x => x.UtcNow).Returns(utcNow);
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<InfraPingMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, InfraPingMessage, CancellationToken>((topic, message, _) =>
            {
                publishedTopic = topic;
                publishedMessage = message;
            })
            .Returns(Task.CompletedTask);

        var sut = new KafkaPingFunction(
            publisher.Object,
            clock.Object,
            environmentName: "local",
            topicName: "mm.infrastructure.ping");

        var result = await sut.HandleAsync();

        Assert.True(result.Success);
        Assert.Equal("mm.infrastructure.ping", result.Topic);
        Assert.Equal("mm.infrastructure.ping", publishedTopic);
        Assert.NotNull(publishedMessage);
        Assert.Equal("INFRA_PING", publishedMessage!.EventType);
        Assert.Equal(utcNow, publishedMessage.Timestamp);
        Assert.Equal("lambda-kafka-ping", publishedMessage.Source);
        Assert.Equal("local", publishedMessage.Environment);
        Assert.False(string.IsNullOrWhiteSpace(publishedMessage.CorrelationId));
        Assert.Equal("generic-matchmaking-infra", publishedMessage.Metadata["service"]);
        Assert.Equal("connectivity-check", publishedMessage.Metadata["purpose"]);

        publisher.Verify(
            x => x.PublishAsync("mm.infrastructure.ping", It.IsAny<InfraPingMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
