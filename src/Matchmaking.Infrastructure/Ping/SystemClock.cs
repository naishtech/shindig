namespace Matchmaking.Infrastructure.Ping;

/// <summary>
/// Reads the current system time for runtime ping operations.
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <summary>
    /// At this point the runtime needs the current UTC timestamp for the outgoing ping event.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
