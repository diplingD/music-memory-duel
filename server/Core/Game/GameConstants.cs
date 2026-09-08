namespace Server.Core.Game;

public static class GameConstants
{
    public const int ComposeMaxMs = 15_000;
    public const double SolveMultiplier = 2.0;
    public const int SolveMinMs = 5_000;
    public const int SolveMaxMs = 15_000;
    public const int ResultDisplayMs = 5_000;

    public const double RhythmTolerance = 0.40;

    public const int MinPlayers = 2;
    public const int RoundsPerPlayer = 3;
}
