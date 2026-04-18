using System.Text.Json;
using Confluent.Kafka;
using Matchmaking.Infrastructure.Ping;
using Matchmaking.ProducerWebService.Matchmaking;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var relativePath = context.Description?.RelativePath;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.CompletedTask;
        }

        switch (relativePath)
        {
            case "matchmaking/join":
                SetJsonRequestExample(operation, new
                {
                    playerId = "player-001",
                    queueName = "default-queue",
                    region = "local-dev",
                    gameMode = "casual-duo",
                    skillBracket = "bronze",
                    attributes = new
                    {
                        preferredRole = "support",
                        inputType = "controller"
                    },
                    metadata = new
                    {
                        ticketId = "ticket-1001",
                        partySize = "2"
                    }
                });
                break;
            case "matchmaking/leave":
                SetJsonRequestExample(operation, new
                {
                    playerId = "player-001",
                    queueName = "default-queue",
                    region = "local-dev",
                    gameMode = "casual-duo",
                    skillBracket = "bronze",
                    reason = "player-requested-leave"
                });
                break;
            case "matchmaking/update":
                SetJsonRequestExample(operation, new
                {
                    playerId = "player-001",
                    queueName = "default-queue",
                    region = "local-dev",
                    gameMode = "casual-duo",
                    skillBracket = "silver",
                    attributes = new
                    {
                        preferredRole = "tank",
                        inputType = "keyboard-mouse"
                    },
                    metadata = new
                    {
                        ticketId = "ticket-1001",
                        updateSource = "party-lobby"
                    }
                });
                break;
            case "matchmaking/cancel":
                SetJsonRequestExample(operation, new
                {
                    playerId = "player-001",
                    queueName = "default-queue",
                    region = "local-dev",
                    gameMode = "casual-duo",
                    skillBracket = "bronze",
                    reason = "player-cancelled-search"
                });
                break;
            case "matchmaking/queues/{queueName}/players":
                SetPathParameterExample(operation, "queueName", "default-queue");
                SetJsonResponseExample(operation, "200", new
                {
                    queueName = "default-queue",
                    players = new[]
                    {
                        new
                        {
                            playerId = "player-001",
                            region = "local-dev",
                            gameMode = "casual-duo",
                            skillBracket = "bronze",
                            attributes = new
                            {
                                preferredRole = "support",
                                inputType = "controller"
                            },
                            metadata = new
                            {
                                ticketId = "ticket-1001",
                                partySize = "2"
                            }
                        }
                    }
                });
                break;
        }

        return Task.CompletedTask;
    });
});
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

app.MapGet("/matchmaking/queues/{queueName}/players", async (
    string queueName,
    IPlayerQueueStateStore stateStore,
    CancellationToken cancellationToken) =>
{
    var players = await stateStore.GetQueuedPlayersAsync(queueName, cancellationToken);

    return Results.Ok(new
    {
        queueName,
        players = players.Select(player => new
        {
            player.PlayerId,
            player.Region,
            player.GameMode,
            player.SkillBracket,
            player.Attributes,
            player.Metadata
        })
    });
})
    .WithName("GetQueuedPlayers")
    .WithTags("Matchmaking")
    .WithSummary("Returns the players currently queued for the requested queue name.")
    .Produces(StatusCodes.Status200OK, contentType: "application/json");

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

static void SetJsonRequestExample(Microsoft.OpenApi.OpenApiOperation operation, object example)
{
    var content = operation.RequestBody?.Content;

    if (content is null)
    {
        return;
    }

    if (content.TryGetValue("application/json", out var mediaType) && mediaType is not null)
    {
        mediaType.Example = JsonSerializer.SerializeToNode(example);
    }
}

static void SetJsonResponseExample(Microsoft.OpenApi.OpenApiOperation operation, string statusCode, object example)
{
    var responses = operation.Responses;

    if (responses is null)
    {
        return;
    }

    if (!responses.TryGetValue(statusCode, out var response) || response is null)
    {
        response = new Microsoft.OpenApi.OpenApiResponse
        {
            Description = "OK"
        };

        responses[statusCode] = response;
    }

    if (response is not Microsoft.OpenApi.OpenApiResponse openApiResponse)
    {
        return;
    }

    openApiResponse.Content ??= new Dictionary<string, Microsoft.OpenApi.OpenApiMediaType>(StringComparer.OrdinalIgnoreCase);

    if (!openApiResponse.Content.TryGetValue("application/json", out var mediaType) || mediaType is null)
    {
        mediaType = new Microsoft.OpenApi.OpenApiMediaType();
        openApiResponse.Content["application/json"] = mediaType;
    }

    mediaType.Example = JsonSerializer.SerializeToNode(example);
}

static void SetPathParameterExample(Microsoft.OpenApi.OpenApiOperation operation, string parameterName, string example)
{
    var parameter = operation.Parameters?
        .OfType<Microsoft.OpenApi.OpenApiParameter>()
        .FirstOrDefault(candidate => string.Equals(candidate.Name, parameterName, StringComparison.OrdinalIgnoreCase));

    if (parameter is not null)
    {
        parameter.Example = JsonSerializer.SerializeToNode(example);
    }
}

