using System.Net;
using System.Text.Json;
using Matchmaking.SDK.Matchmaking;
using Xunit;

namespace Matchmaking.Infrastructure.Tests;

/// <summary>
/// Verifies the game-developer HTTP client sends the expected matchmaking API requests.
/// </summary>
public sealed class MatchmakingApiClientTests
{
    [Fact]
    public async Task QueuePlayerAsync_PostsExpectedPayloadToJoinEndpoint()
    {
        var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://matchmaking.example/")
        };

        var sut = new MatchmakingApiClient(httpClient);

        await sut.QueuePlayerAsync(
            playerId: "player-123",
            queueName: "default",
            region: "oce",
            gameMode: "duo",
            skillBracket: "1400-1499",
            attributes: new Dictionary<string, string> { ["latency"] = "32" },
            metadata: new Dictionary<string, string> { ["gameId"] = "game-xyz" },
            CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://matchmaking.example/matchmaking/join", handler.Request.RequestUri!.ToString());

        var payload = await handler.Request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("player-123", document.RootElement.GetProperty("playerId").GetString());
        Assert.Equal("default", document.RootElement.GetProperty("queueName").GetString());
        Assert.Equal("oce", document.RootElement.GetProperty("region").GetString());
        Assert.Equal("duo", document.RootElement.GetProperty("gameMode").GetString());
        Assert.Equal("1400-1499", document.RootElement.GetProperty("skillBracket").GetString());
        Assert.Equal("32", document.RootElement.GetProperty("attributes").GetProperty("latency").GetString());
        Assert.Equal("game-xyz", document.RootElement.GetProperty("metadata").GetProperty("gameId").GetString());
    }

    [Fact]
    public async Task GetQueuedPlayersAsync_CallsQueueInspectionEndpointAndReturnsPayload()
    {
        const string responseBody = """
            {
              "queueName": "default",
              "players": [
                {
                  "playerId": "player-123",
                  "region": "oce",
                  "gameMode": "duo",
                  "skillBracket": "1400-1499",
                  "attributes": {
                    "latency": "32"
                  },
                  "metadata": {
                    "gameId": "game-xyz"
                  }
                }
              ]
            }
            """;

        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, responseBody);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://matchmaking.example/")
        };

        var sut = new MatchmakingApiClient(httpClient);

        using var document = await sut.GetQueuedPlayersAsync("default", CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("https://matchmaking.example/matchmaking/queues/default/players", handler.Request.RequestUri!.ToString());
        Assert.Equal("default", document.RootElement.GetProperty("queueName").GetString());
        Assert.Equal("player-123", document.RootElement.GetProperty("players")[0].GetProperty("playerId").GetString());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _responseBody;

        public CapturingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.Accepted, string? responseBody = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                RequestMessage = request,
                Content = _responseBody is null ? null : new StringContent(_responseBody)
            });
        }
    }
}
