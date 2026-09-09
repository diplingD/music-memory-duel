namespace Server.Core.Game;

public static class GameConstants
{
    public const int ComposeMaxMs = 15_000;
    public const double SolveMultiplier = 2.0;
    public const int SolveMinMs = 5_000;
    public const int SolveMaxMs = 15_000;
    public const int ResultDisplayMs = 5_000;

    public const double RhythmTolerance = 0.40;

    public const int MinNotes = 1;
    public const int MaxNotes = 100;

    public const int MinPlayers = 2;
    public const int RoundsPerPlayer = 3;

    // 70/10s covers a full MaxNotes-length melody spread over ComposeMaxMs (15s) plus the
    // final SubmitSequence call, without being anywhere near a real spam-flood threat.
    public const int RateLimitMaxCalls = 70;
    public const int RateLimitWindowMs = 10_000;
}
