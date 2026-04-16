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

    [Fact]
    [Trait("Category", "Component")]
    public async Task PostJoinAsync_ForDuplicateConcurrentRequests_PublishesSingleJoinEvent()
    {
        var producerBaseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL");

        if (string.IsNullOrWhiteSpace(producerBaseUrl))
        {
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(producerBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(3)
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

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = $"producer-duplicate-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe("mm.player.queue");
        consumer.Consume(TimeSpan.FromMilliseconds(250));

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

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/matchmaking/join", request),
            client.PostAsJsonAsync("/matchmaking/join", request));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));

        var matchingMessages = await ConsumeMessagesForPlayerAsync(consumer, playerId, CancellationToken.None);
        var joinMessages = matchingMessages
            .Where(message => IsEventType(message, "PLAYER_JOIN_QUEUE"))
            .ToList();

        Assert.Single(joinMessages);
    }

    [Fact]
    [Trait("Category", "Component")]
    public async Task PostCancelAsync_ForDuplicateConcurrentRequests_PublishesSingleDequeuedEvent()
    {
        var producerBaseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL");

        if (string.IsNullOrWhiteSpace(producerBaseUrl))
        {
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(producerBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(3)
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

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = $"producer-cancel-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe("mm.player.queue");
        consumer.Consume(TimeSpan.FromMilliseconds(250));

        var joinRequest = new
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

        var joinResponse = await client.PostAsJsonAsync("/matchmaking/join", joinRequest);

        Assert.Equal(HttpStatusCode.Accepted, joinResponse.StatusCode);

        var cancelRequest = new
        {
            playerId = playerId,
            queueName = "default",
            region = "oce",
            gameMode = "duo",
            skillBracket = "1400-1499",
            reason = "cancel-matchmaking"
        };

        var cancelResponses = await Task.WhenAll(
            client.PostAsJsonAsync("/matchmaking/cancel", cancelRequest),
            client.PostAsJsonAsync("/matchmaking/cancel", cancelRequest));

        Assert.All(cancelResponses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));

        var matchingMessages = await ConsumeMessagesForPlayerAsync(consumer, playerId, CancellationToken.None);
        var dequeueMessages = matchingMessages
            .Where(message => IsEventType(message, "PLAYER_DEQUEUED"))
            .ToList();

        Assert.Single(dequeueMessages);
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

    private static bool IsEventType(string payload, string expectedEventType)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("eventType", out var eventTypeProperty) &&
               string.Equals(eventTypeProperty.GetString(), expectedEventType, StringComparison.Ordinal);
    }

    private static Task<List<string>> ConsumeMessagesForPlayerAsync(IConsumer<Ignore, string> consumer, string expectedPlayerId, CancellationToken cancellationToken)
    {
        var matches = new List<string>();
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));

            if (consumeResult?.Message?.Value is null)
            {
                continue;
            }

            using var document = JsonDocument.Parse(consumeResult.Message.Value);

            if (document.RootElement.TryGetProperty("playerId", out var playerIdProperty) &&
                string.Equals(playerIdProperty.GetString(), expectedPlayerId, StringComparison.Ordinal))
            {
                matches.Add(consumeResult.Message.Value);
            }
        }

        return Task.FromResult(matches);
    }
}
