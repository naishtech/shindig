namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Represents a request to remove a player from a matchmaking queue.
/// </summary>
public sealed record CancelQueueRequest(
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    string Reason);
