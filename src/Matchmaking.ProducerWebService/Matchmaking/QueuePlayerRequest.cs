namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Represents a request to place a player into a matchmaking queue.
/// </summary>
public sealed record QueuePlayerRequest(
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    IReadOnlyDictionary<string, string>? Attributes,
    IReadOnlyDictionary<string, string>? Metadata);
