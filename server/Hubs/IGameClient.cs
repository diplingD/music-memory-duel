using Server.Core.DTOs;
using Server.Core.Models;

namespace Server.Hubs;

// Defines methods that frontend will handle
public interface IGameClient
{
    Task PlayerListChanged(PlayerDto[] players);
    Task MatchStarted(long composeDeadlineUnixMs);
    Task NotePlayed(NoteEvent note);
    Task SolvingStarted(Guid roundId, long solveDeadlineUnixMs);
    Task RoundEnded(RoundEndedDto dto);
    Task ErrorOccurred(string code, string message);
}
