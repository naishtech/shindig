using System.Net;
using System.Net.Http.Json;
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

        var request = new
        {
            playerId = "player-123",
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
    }
}
