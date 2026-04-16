namespace Matchmaking.ConsumerWorker.Matchmaking;

/// <summary>
/// Represents a matched player included in a created match event.
/// </summary>
public sealed record MatchedPlayer(
    string PlayerId,
    IReadOnlyDictionary<string, string> Attributes);
