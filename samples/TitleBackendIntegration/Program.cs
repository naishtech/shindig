using Matchmaking.SDK.Matchmaking;

const string DefaultQueueName = "default";
const string DefaultRegion = "oce";
const string DefaultGameMode = "duo";
const string DefaultSkillBracket = "1400-1499";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<MatchmakingApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Matchmaking:BaseUrl"]
        ?? throw new InvalidOperationException("Matchmaking:BaseUrl must be configured by the title backend."));
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "TitleBackendIntegration",
    message = "Sample title backend routes for all Shindig SDK operations.",
    routes = new[]
    {
        "POST /queue/{playerId}",
        "POST /queue/{playerId}/update",
        "POST /queue/{playerId}/leave",
        "POST /queue/{playerId}/cancel",
        "GET /queue/{queueName}/players"
    }
}));

app.MapPost("/queue/{playerId}", async (
    string playerId,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    await matchmakingClient.QueuePlayerAsync(
        playerId: playerId,
        queueName: DefaultQueueName,
        region: DefaultRegion,
        gameMode: DefaultGameMode,
        skillBracket: DefaultSkillBracket,
        attributes: new Dictionary<string, string>
        {
            ["latency"] = "32",
            ["mmr"] = "1450"
        },
        metadata: new Dictionary<string, string>
        {
            ["gameId"] = "sample-title",
            ["operation"] = "queue"
        },
        cancellationToken: cancellationToken);

    return Results.Accepted(new
    {
        playerId,
        message = "Queued through the Shindig matchmaking API."
    });
});

app.MapPost("/queue/{playerId}/update", async (
    string playerId,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    await matchmakingClient.UpdatePlayerAsync(
        playerId: playerId,
        queueName: DefaultQueueName,
        region: DefaultRegion,
        gameMode: DefaultGameMode,
        skillBracket: DefaultSkillBracket,
        attributes: new Dictionary<string, string>
        {
            ["latency"] = "28",
            ["mmr"] = "1460"
        },
        metadata: new Dictionary<string, string>
        {
            ["gameId"] = "sample-title",
            ["operation"] = "update"
        },
        cancellationToken: cancellationToken);

    return Results.Accepted(new
    {
        playerId,
        message = "Updated queued player details through the Shindig matchmaking API."
    });
});

app.MapPost("/queue/{playerId}/leave", async (
    string playerId,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    await matchmakingClient.LeaveQueueAsync(
        playerId: playerId,
        queueName: DefaultQueueName,
        region: DefaultRegion,
        gameMode: DefaultGameMode,
        skillBracket: DefaultSkillBracket,
        reason: "player-left-lobby",
        cancellationToken: cancellationToken);

    return Results.Accepted(new
    {
        playerId,
        message = "Removed player from the queue through the Shindig matchmaking API."
    });
});

app.MapPost("/queue/{playerId}/cancel", async (
    string playerId,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    await matchmakingClient.CancelQueueAsync(
        playerId: playerId,
        queueName: DefaultQueueName,
        region: DefaultRegion,
        gameMode: DefaultGameMode,
        skillBracket: DefaultSkillBracket,
        reason: "player-cancelled-search",
        cancellationToken: cancellationToken);

    return Results.Accepted(new
    {
        playerId,
        message = "Cancelled matchmaking through the Shindig matchmaking API."
    });
});

app.MapGet("/queue/{queueName}/players", async (
    string queueName,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    using var queuedPlayers = await matchmakingClient.GetQueuedPlayersAsync(queueName, cancellationToken);

    return Results.Text(queuedPlayers.RootElement.GetRawText(), contentType: "application/json");
});

app.Run();
