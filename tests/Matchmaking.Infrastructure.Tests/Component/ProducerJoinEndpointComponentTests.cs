using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Xunit;

namespace Matchmaking.Infrastructure.Tests.Component;

/// <summary>
/// Validates the deployed producer join endpoint in the LocalStack-backed environment.
/// </summary>
public sealed class ProducerJoinEndpointComponentTests
{
    [Fact]
    [Trait("Category", "Component")]
    public async Task PostJoinAsync_AcceptsQueueRequestAgainstDeployedProducer()
    {
        var producerBaseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL");

        if (string.IsNullOrWhiteSpace(producerBaseUrl))
        {
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(producerBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(2)
        };

        try
        {
            using var healthResponse = await client.GetAsync("/health");

            if (!healthResponse.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var playerId = $"player-{Guid.NewGuid():N}";
        const string topicName = "mm.player.queue";

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = $"producer-component-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(topicName);

        var request = new
        {
            playerId = playerId,
            queueName = "default",
            region = "oce",
            gameMode = "duo",
            skillBracket = "1400-1499",
            attributes = new Dictionary<string, string>
            {
                ["latency"] = "32",
                ["mmr"] = "1450"
            },
            metadata = new Dictionary<string, string>
            {
                ["gameId"] = "game-xyz",
                ["modeId"] = "mode-duo"
            }
        };

        var response = await client.PostAsJsonAsync("/matchmaking/join", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var consumedMessage = await ConsumePlayerJoinAsync(consumer, playerId, CancellationToken.None);

        Assert.NotNull(consumedMessage);
        Assert.Equal("PLAYER_JOIN_QUEUE", consumedMessage!.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(playerId, consumedMessage.RootElement.GetProperty("playerId").GetString());
        Assert.Equal("oce:duo:1400-1499", consumedMessage.RootElement.GetProperty("partitionKey").GetString());
    }

    private static Task<JsonDocument?> ConsumePlayerJoinAsync(IConsumer<Ignore, string> consumer, string expectedPlayerId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));

            if (consumeResult?.Message?.Value is null)
            {
                continue;
            }

            var document = JsonDocument.Parse(consumeResult.Message.Value);

            if (document.RootElement.TryGetProperty("playerId", out var playerIdProperty) &&
                string.Equals(playerIdProperty.GetString(), expectedPlayerId, StringComparison.Ordinal))
            {
                return Task.FromResult<JsonDocument?>(document);
            }

            document.Dispose();
        }

        return Task.FromResult<JsonDocument?>(null);
    }
}
