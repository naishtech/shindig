using Matchmaking.ConsumerWorker.Matchmaking;

namespace Matchmaking.ConsumerWorker;

/// <summary>
/// Hosts the matchmaking consumer worker and its infrastructure dependencies.
/// </summary>
public static class ConsumerWorkerProgram
{
    /// <summary>
    /// At this point the worker host is being configured for Kafka consumption and Redis-backed pooling.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<KafkaWorkerOptions>(builder.Configuration.GetSection("Kafka"));
        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<KafkaWorkerOptions>>().Value);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RedisOptions>>().Value);
        builder.Services.AddSingleton<ISystemClock, SystemClock>();
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<RedisOptions>();

            return ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectRetry = 2,
                ConnectTimeout = 2000,
                SyncTimeout = 2000,
                EndPoints = { options.Endpoint }
            });
        });
        builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var options = sp.GetRequiredService<KafkaWorkerOptions>();

            return new ProducerBuilder<Null, string>(new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = options.GroupId,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build();
        });
        builder.Services.AddSingleton<IMatchmakingPoolStore, RedisMatchmakingPoolStore>();
        builder.Services.AddSingleton<IMatchResultPublisher, KafkaMatchResultPublisher>();
        builder.Services.AddSingleton<MatchMakerConsumerWorker>();
        builder.Services.AddHostedService<KafkaQueueConsumerBackgroundService>();

        var host = builder.Build();
        host.Run();
    }
}
