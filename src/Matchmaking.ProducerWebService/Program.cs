using Confluent.Kafka;
using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<KafkaProducerOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;

    return ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        ConnectRetry = 2,
        ConnectTimeout = 2000,
        SyncTimeout = 2000,
        EndPoints = { options.Endpoint }
    });
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

    return new ProducerBuilder<Null, string>(new ProducerConfig
    {
        BootstrapServers = options.BootstrapServers,
        ClientId = options.ClientId,
        Acks = Acks.All,
        EnableIdempotence = true
    }).Build();
});
builder.Services.AddSingleton<IMatchmakingEventPublisher, KafkaMatchmakingEventPublisher>();
builder.Services.AddSingleton<IPlayerQueueStateStore, RedisPlayerQueueStateStore>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

    return new MatchmakingRequestHandler(
        sp.GetRequiredService<IMatchmakingEventPublisher>(),
        sp.GetRequiredService<ISystemClock>(),
        sp.GetRequiredService<IPlayerQueueStateStore>(),
        options.TopicName);
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

var healthPayload = new
{
    service = "MatchMakerProducerWebService",
    status = "healthy"
};

app.MapGet("/", () => Results.Ok(healthPayload))
    .WithName("GetProducerRoot")
    .WithSummary("Returns the producer service status.");

app.MapGet("/health", () => Results.Ok(healthPayload))
    .WithName("GetProducerHealth")
    .WithSummary("Returns the producer health payload.");

app.MapPost("/matchmaking/join", async (
    QueuePlayerRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.QueuePlayerAsync(request, cancellationToken);
    return Results.Accepted();
})
    .WithName("JoinMatchmakingQueue")
    .WithTags("Matchmaking")
    .WithSummary("Queues a player for matchmaking.");

app.MapPost("/matchmaking/leave", async (
    LeaveQueueRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.LeaveQueueAsync(request, cancellationToken);
    return Results.Accepted();
})
    .WithName("LeaveMatchmakingQueue")
    .WithTags("Matchmaking")
    .WithSummary("Removes a player from the matchmaking queue.");

app.MapPost("/matchmaking/update", async (
    UpdatePlayerRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.UpdatePlayerAsync(request, cancellationToken);
    return Results.Accepted();
})
    .WithName("UpdateQueuedPlayer")
    .WithTags("Matchmaking")
    .WithSummary("Updates a queued player's matchmaking attributes.");

app.MapPost("/matchmaking/cancel", async (
    CancelQueueRequest request,
    MatchmakingRequestHandler handler,
    CancellationToken cancellationToken) =>
{
    await handler.CancelQueueAsync(request, cancellationToken);
    return Results.Accepted();
})
    .WithName("CancelMatchmakingQueueEntry")
    .WithTags("Matchmaking")
    .WithSummary("Cancels a player's matchmaking request.");

app.Run();

