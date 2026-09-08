namespace Server.Core.DTOs;

public record PlayerRoundResultDto(
    string PlayerId, bool Submitted, bool Correct, double PrefixRatio, int PointsDelta, string Reason);
