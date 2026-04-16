namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Represents a match emitted by the consumer worker for downstream systems.
/// </summary>
public sealed record MatchCreatedEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string MatchId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    string PartitionKey,
    IReadOnlyList<MatchedPlayer> Players,
    IReadOnlyDictionary<string, string> Metadata);
