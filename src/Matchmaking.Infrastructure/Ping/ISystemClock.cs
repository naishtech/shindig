namespace Matchmaking.Infrastructure.Ping;

/// <summary>
/// Provides the current UTC time for infrastructure ping processing.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// At this point the ping event is being timestamped for publishing.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
