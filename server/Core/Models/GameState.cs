namespace Server.Core.Models;

public sealed record GameState
{
    public required Phase Phase { get; init; }
    public required IReadOnlyList<Player> Players { get; init; }
    public Round? CurrentRound { get; init; }
    public int ComposerIndex { get; init; }     // index into Players (join order) for round-robin rotation
    public int TotalRounds { get; init; }       // fixed at match start: Players.Count * GameConstants.RoundsPerPlayer
    public int RoundsPlayed { get; init; }
}
