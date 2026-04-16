using Confluent.Kafka;
using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KafkaProducerOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
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
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

    return new MatchmakingRequestHandler(
        sp.GetRequiredService<IMatchmakingEventPublisher>(),
        sp.GetRequiredService<ISystemClock>(),
        options.TopicName);
});

var app = builder.Build();

var healthPayload = new
{
    service = "MatchMakerProducerWebService",
    status = "healthy"
};

app.MapGet("/", () => Results.Ok(healthPayload));
app.MapGet("/health", () => Results.Ok(healthPayload));

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

public partial class Program;
