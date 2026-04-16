namespace Matchmaking.ProducerWebService.Matchmaking;

/// <summary>
/// Represents a matchmaking lifecycle event published by the producer web service.
/// </summary>
public sealed record MatchmakingEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    string PartitionKey,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> Metadata,
    string? Reason = null);
