namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Represents a request for a player to leave a matchmaking queue.
/// </summary>
public sealed record LeaveQueueRequest(
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    string Reason);
