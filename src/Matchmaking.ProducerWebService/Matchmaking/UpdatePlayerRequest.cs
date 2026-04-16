namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Represents a request to update a queued player's matchmaking attributes.
/// </summary>
public sealed record UpdatePlayerRequest(
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    IReadOnlyDictionary<string, string>? Attributes,
    IReadOnlyDictionary<string, string>? Metadata);
