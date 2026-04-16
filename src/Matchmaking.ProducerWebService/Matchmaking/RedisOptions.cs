namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Stores the Redis connection settings used for request coordination.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>
    /// At this point the application is preparing to connect to the Redis coordination endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "localhost:6379";
}
