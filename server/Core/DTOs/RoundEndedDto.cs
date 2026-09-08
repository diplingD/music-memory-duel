namespace Server.Core.DTOs;

public record RoundEndedDto(
    Guid RoundId, string CreatorId, bool CreatorConfirmed,
    PlayerRoundResultDto[] Results, StandingDto[] Standings,
    long ResultDisplayDeadlineUnixMs);
