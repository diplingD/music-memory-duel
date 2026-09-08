namespace Server.Core.Models;

public sealed record GameState
{
    public required Phase Phase { get; init; }
    public required IReadOnlyList<Player> Players { get; init; }
    public Round? CurrentRound { get; init; }
}
