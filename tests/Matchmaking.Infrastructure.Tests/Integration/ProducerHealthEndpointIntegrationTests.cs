using System.Net;
using System.Text.Json;
using Matchmaking.ProducerWebService.Matchmaking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Matchmaking.Infrastructure.Tests.Integration;

/// <summary>
/// Validates the producer web service in its hosted HTTP shape before LocalStack-backed component coverage is added.
/// </summary>
public sealed class ProducerHealthEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// At this point the hosted producer service is being prepared for integration-level verification.
    /// </summary>
    public ProducerHealthEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthAsync_ReturnsHealthyProducerStatus()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("MatchMakerProducerWebService", document.RootElement.GetProperty("service").GetString());
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOpenApiDocumentAsync_ExposesMatchmakingJoinEndpoint()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/matchmaking/join", out _));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetApiReferencePageAsync_ReturnsInteractiveApiPage()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Scalar", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOpenApiDocumentAsync_ProvidesSensibleMatchmakingExamples()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var joinExample = paths
            .GetProperty("/matchmaking/join")
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");

        Assert.Equal("player-001", joinExample.GetProperty("playerId").GetString());
        Assert.Equal("default-queue", joinExample.GetProperty("queueName").GetString());
        Assert.Equal("support", joinExample.GetProperty("attributes").GetProperty("preferredRole").GetString());

        var cancelExample = paths
            .GetProperty("/matchmaking/cancel")
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");

        Assert.Equal("player-cancelled-search", cancelExample.GetProperty("reason").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetQueuedPlayersAsync_ReturnsPlayersForRequestedQueue()
    {
        var stateStore = new Mock<IPlayerQueueStateStore>();
        stateStore
            .Setup(x => x.GetQueuedPlayersAsync("default-queue", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new QueuePlayerRequest(
                    "player-001",
                    "default-queue",
                    "local-dev",
                    "casual-duo",
                    "bronze",
                    new Dictionary<string, string> { ["preferredRole"] = "support" },
                    new Dictionary<string, string> { ["ticketId"] = "ticket-1001" })
            ]);

        using var client = await CreateClientAsync(services =>
        {
            services.RemoveAll<IPlayerQueueStateStore>();
            services.AddSingleton(stateStore.Object);
        });

        var response = await client.GetAsync("/matchmaking/queues/default-queue/players");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("default-queue", document.RootElement.GetProperty("queueName").GetString());

        var player = document.RootElement.GetProperty("players")[0];
        Assert.Equal("player-001", player.GetProperty("playerId").GetString());
        Assert.Equal("local-dev", player.GetProperty("region").GetString());
        Assert.Equal("support", player.GetProperty("attributes").GetProperty("preferredRole").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOpenApiDocumentAsync_ProvidesQueueLookupExamples()
    {
        using var client = await CreateClientAsync();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var lookupOperation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/matchmaking/queues/{queueName}/players")
            .GetProperty("get");

        var parameter = lookupOperation.GetProperty("parameters")[0];
        Assert.Equal("queueName", parameter.GetProperty("name").GetString());
        Assert.Equal("default-queue", parameter.GetProperty("example").GetString());

        var responseExample = lookupOperation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");

        Assert.Equal("default-queue", responseExample.GetProperty("queueName").GetString());
        Assert.Equal("player-001", responseExample.GetProperty("players")[0].GetProperty("playerId").GetString());
    }

    private async Task<HttpClient> CreateClientAsync(Action<IServiceCollection>? configureServices = null)
    {
        if (configureServices is not null)
        {
            return _factory
                .WithWebHostBuilder(builder => builder.ConfigureServices(configureServices))
                .CreateClient();
        }

        var producerBaseUrl = Environment.GetEnvironmentVariable("PRODUCER_BASE_URL");

        if (!string.IsNullOrWhiteSpace(producerBaseUrl))
        {
            var deployedClient = new HttpClient
            {
                BaseAddress = new Uri(producerBaseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(2)
            };

            try
            {
                using var response = await deployedClient.GetAsync("/health");

                if (response.IsSuccessStatusCode)
                {
                    return deployedClient;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            deployedClient.Dispose();
        }

        return _factory.CreateClient();
    }
}
