using Matchmaking.Infrastructure.Ping;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

public class KafkaPingFunctionTests
{
    [Fact]
    public async Task HandleAsync_PublishesInfrastructurePingEventToConfiguredTopic()
    {
        var publisher = new InMemoryKafkaPingPublisher();
        var clock = new FixedClock(new DateTimeOffset(2026, 4, 15, 5, 0, 0, TimeSpan.Zero));
        var sut = new KafkaPingFunction(
            publisher,
            clock,
            environmentName: "local",
            topicName: "mm.infrastructure.ping");

        var result = await sut.HandleAsync();

        Assert.True(result.Success);
        Assert.Equal("mm.infrastructure.ping", result.Topic);
        Assert.Single(publisher.Messages);

        var message = publisher.Messages[0];
        Assert.Equal("INFRA_PING", message.EventType);
        Assert.Equal(clock.UtcNow, message.Timestamp);
        Assert.Equal("lambda-kafka-ping", message.Source);
        Assert.Equal("local", message.Environment);
        Assert.False(string.IsNullOrWhiteSpace(message.CorrelationId));
        Assert.Equal("generic-matchmaking-infra", message.Metadata["service"]);
        Assert.Equal("connectivity-check", message.Metadata["purpose"]);
    }

    private sealed class InMemoryKafkaPingPublisher : IPingPublisher
    {
        public List<InfraPingMessage> Messages { get; } = new();

        public Task PublishAsync(string topic, InfraPingMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
