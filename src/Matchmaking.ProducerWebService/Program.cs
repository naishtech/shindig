using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IMatchmakingEventPublisher, LoggingMatchmakingEventPublisher>();
builder.Services.AddSingleton(sp => new MatchmakingRequestHandler(
    sp.GetRequiredService<IMatchmakingEventPublisher>(),
    sp.GetRequiredService<ISystemClock>(),
    builder.Configuration["Kafka:TopicName"] ?? "mm.player.queue"));

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "MatchMakerProducerWebService",
    status = "healthy"
}));

app.MapPost("/matchmaking/join", async (
    QueuePlayerRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.QueuePlayerAsync(request, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/matchmaking/leave", async (
    LeaveQueueRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.LeaveQueueAsync(request, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/matchmaking/update", async (
    UpdatePlayerRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.UpdatePlayerAsync(request, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/matchmaking/cancel", async (
    CancelQueueRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.CancelQueueAsync(request, cancellationToken);
    return Results.Accepted();
});

app.Run();
