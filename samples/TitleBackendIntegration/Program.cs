using Matchmaking.SDK.Matchmaking;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<MatchmakingApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Matchmaking:BaseUrl"]
        ?? throw new InvalidOperationException("Matchmaking:BaseUrl must be configured by the title backend."));
});

var app = builder.Build();

app.MapPost("/queue/{playerId}", async (
    string playerId,
    MatchmakingApiClient matchmakingClient,
    CancellationToken cancellationToken) =>
{
    await matchmakingClient.QueuePlayerAsync(
        playerId: playerId,
        queueName: "default",
        region: "oce",
        gameMode: "duo",
        skillBracket: "1400-1499",
        attributes: new Dictionary<string, string>
        {
            ["latency"] = "32",
            ["mmr"] = "1450"
        },
        metadata: new Dictionary<string, string>
        {
            ["gameId"] = "sample-title"
        },
        cancellationToken: cancellationToken);

    return Results.Accepted(new
    {
        playerId,
        message = "Queued through the Shindig matchmaking API."
    });
});

app.Run();
