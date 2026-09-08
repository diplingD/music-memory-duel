namespace Server.Core.Game;

public sealed record PlayerRoundResult(
    string PlayerId,
    bool Submitted,
    bool Correct,
    double PrefixRatio,
    int PointsDelta,
    string Reason);
