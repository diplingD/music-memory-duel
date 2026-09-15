using Server.Core.Models;

namespace Server.Core.DTOs;

public record RoomSnapshotDto(
    Phase Phase,
    PlayerDto[] Players,
    string? ComposerId,
    Guid? RoundId,
    long? PhaseDeadlineUnixMs,
    StandingDto[] Standings
);
