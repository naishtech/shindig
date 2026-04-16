namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Represents a player currently waiting in a matchmaking partition.
/// </summary>
public sealed record PlayerPoolEntry(
    string PlayerId,
    string QueueName,
    string Region,
    string GameMode,
    string SkillBracket,
    string PartitionKey,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset JoinedAt,
    DateTimeOffset UpdatedAt);
