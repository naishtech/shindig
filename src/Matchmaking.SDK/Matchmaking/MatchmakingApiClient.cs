using System.Net.Http.Json;
using System.Text.Json;

namespace Matchmaking.SDK.Matchmaking;

/// <summary>
/// Provides a simple HTTP client for title backends integrating with the matchmaking service.
/// </summary>
public sealed class MatchmakingApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// At this point the client has been configured with the matchmaking service base address.
    /// </summary>
    public MatchmakingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// At this point a title backend is submitting a player to the matchmaking queue.
    /// </summary>
    public Task QueuePlayerAsync(
        string playerId,
        string queueName,
        string region,
        string gameMode,
        string skillBracket,
        IReadOnlyDictionary<string, string>? attributes,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        return PostAsync(
            "/matchmaking/join",
            new
            {
                playerId,
                queueName,
                region,
                gameMode,
                skillBracket,
                attributes = attributes ?? new Dictionary<string, string>(),
                metadata = metadata ?? new Dictionary<string, string>()
            },
            cancellationToken);
    }

    /// <summary>
    /// At this point a queued player's matchmaking attributes are being refreshed.
    /// </summary>
    public Task UpdatePlayerAsync(
        string playerId,
        string queueName,
        string region,
        string gameMode,
        string skillBracket,
        IReadOnlyDictionary<string, string>? attributes,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        return PostAsync(
            "/matchmaking/update",
            new
            {
                playerId,
                queueName,
                region,
                gameMode,
                skillBracket,
                attributes = attributes ?? new Dictionary<string, string>(),
                metadata = metadata ?? new Dictionary<string, string>()
            },
            cancellationToken);
    }

    /// <summary>
    /// At this point a title backend is removing a player from the matchmaking queue.
    /// </summary>
    public Task LeaveQueueAsync(
        string playerId,
        string queueName,
        string region,
        string gameMode,
        string skillBracket,
        string reason,
        CancellationToken cancellationToken)
    {
        return PostAsync(
            "/matchmaking/leave",
            new
            {
                playerId,
                queueName,
                region,
                gameMode,
                skillBracket,
                reason
            },
            cancellationToken);
    }

    /// <summary>
    /// At this point a title backend is cancelling an active matchmaking request.
    /// </summary>
    public Task CancelQueueAsync(
        string playerId,
        string queueName,
        string region,
        string gameMode,
        string skillBracket,
        string reason,
        CancellationToken cancellationToken)
    {
        return PostAsync(
            "/matchmaking/cancel",
            new
            {
                playerId,
                queueName,
                region,
                gameMode,
                skillBracket,
                reason
            },
            cancellationToken);
    }

    /// <summary>
    /// At this point a title backend is reading the currently queued players for a named queue.
    /// </summary>
    public async Task<JsonDocument> GetQueuedPlayersAsync(string queueName, CancellationToken cancellationToken)
    {
        var encodedQueueName = Uri.EscapeDataString(queueName);
        var response = await _httpClient.GetAsync($"/matchmaking/queues/{encodedQueueName}/players", cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task PostAsync(string relativePath, object payload, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(relativePath, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
