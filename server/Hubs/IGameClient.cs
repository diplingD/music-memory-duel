using Server.Core.DTOs;
using Server.Core.Models;

namespace Server.Hubs;

// Defines methods that frontend will handle
public interface IGameClient
{
    Task PlayerListChanged(PlayerDto[] players);
    Task MatchStarted(long composeDeadlineUnixMs, string composerId);
    Task NotePlayed(NoteEvent note);
    Task SolvingStarted(Guid roundId, long solveDeadlineUnixMs);
    Task RoundEnded(RoundEndedDto dto);
    Task MatchEnded(StandingDto[] finalStandings);
    Task ErrorOccurred(string code, string message);
}
