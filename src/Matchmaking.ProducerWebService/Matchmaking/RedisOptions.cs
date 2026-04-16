namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Stores the Redis connection settings used for request coordination.
/// </summary>
public sealed class RedisOptions
{
    /// <summary>
    /// Gets or sets the Redis endpoint used for request coordination.
    /// </summary>
    public string Endpoint { get; set; } = "localhost:6379";
}
