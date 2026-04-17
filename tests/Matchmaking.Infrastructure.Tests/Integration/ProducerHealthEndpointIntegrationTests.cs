using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

    private async Task<HttpClient> CreateClientAsync()
    {
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
