namespace Server.Core.DTOs;

public record RoundEndedDto(
    Guid RoundId, string ComposerId, bool ComposerConfirmed,
    PlayerRoundResultDto[] Results, StandingDto[] Standings,
    long ResultDisplayDeadlineUnixMs);
