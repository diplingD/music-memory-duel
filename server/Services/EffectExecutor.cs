using Microsoft.AspNetCore.SignalR;
using Server.Core.DTOs;
using Server.Core.Game;
using Server.Core.Models;
using Server.Hubs;

namespace Server.Services;

// Only place that knows how an Effect (data) turns into a real action out in the world.
public sealed class EffectExecutor(IHubContext<GameHub, IGameClient> hub, Scheduler scheduler)
{
    public Task ExecuteAsync(Effect effect, RoomActor actor)
    {
        switch (effect)
        {
            case PlayerListChangedEffect playerListChanged:
                return BroadcastPlayerList(playerListChanged, actor);

            case ErrorOccurred error:
                return SendError(error, actor);

            case MatchStartedEffect matchStarted:
                return BroadcastMatchStarted(matchStarted, actor);

            case NotePlayedEffect notePlayed:
                return BroadcastNotePlayed(notePlayed, actor);

            case SolvingStartedEffect solvingStarted:
                return BroadcastSolvingStarted(solvingStarted, actor);

            case RoundEndedEffect roundEnded:
                return BroadcastRoundEnded(roundEnded, actor);

            case MatchEndedEffect matchEnded:
                return BroadcastMatchEnded(matchEnded, actor);

            case ScheduleEffect schedule:
                return ScheduleWakeUp(schedule, actor);

            default:
                return Task.CompletedTask;
        }
    }

    private Task BroadcastPlayerList(PlayerListChangedEffect effect, RoomActor actor)
    {
        var players = effect.Players
            .Select(p => new PlayerDto(p.Id, p.Nick, p.Id == actor.HostId))     // mapping each player to PlayerDto
            .ToArray();

        return hub.Clients.Group(actor.RoomCode).PlayerListChanged(players);
    }

    private Task SendError(ErrorOccurred effect, RoomActor actor)
    {
        return hub.Clients.Group(actor.RoomCode).ErrorOccurred(effect.Code, effect.Message);
    }

    private Task ScheduleWakeUp(ScheduleEffect effect, RoomActor actor)
    {
        scheduler.Schedule(effect.WakeAtUtc, () => actor.PostAsync(effect.EventToRaise).AsTask());  // add SolveDeadlineReached event
        return Task.CompletedTask;
    }

    private Task BroadcastMatchStarted(MatchStartedEffect effect, RoomActor actor)
    {
        var deadlineMs = new DateTimeOffset(effect.ComposeDeadlineUtc).ToUnixTimeMilliseconds();
        return hub.Clients.Group(actor.RoomCode).MatchStarted(deadlineMs, effect.ComposerId);
    }

    // GroupExcept the sender — the player who played the note already heard it locally.
    private Task BroadcastNotePlayed(NotePlayedEffect effect, RoomActor actor)
    {
        return hub.Clients.GroupExcept(actor.RoomCode, effect.ConnectionId).NotePlayed(effect.Note);
    }

    private Task BroadcastSolvingStarted(SolvingStartedEffect effect, RoomActor actor)
    {
        var deadlineMs = new DateTimeOffset(effect.SolveDeadlineUtc).ToUnixTimeMilliseconds();
        return hub.Clients.Group(actor.RoomCode).SolvingStarted(effect.RoundId, deadlineMs);
    }

    private Task BroadcastRoundEnded(RoundEndedEffect effect, RoomActor actor)
    {
        var results = effect.Results
            .Select(r => new PlayerRoundResultDto(r.PlayerId, r.Submitted, r.Correct, r.PrefixRatio, r.PointsDelta, r.Reason))
            .ToArray();

        var standings = BuildStandings(effect.Players);

        var resultDisplayDeadlineMs = new DateTimeOffset(effect.ResultDisplayDeadlineUtc).ToUnixTimeMilliseconds();
        var dto = new RoundEndedDto(effect.RoundId, effect.ComposerId, effect.ComposerConfirmed, results, standings, resultDisplayDeadlineMs);
        return hub.Clients.Group(actor.RoomCode).RoundEnded(dto);
    }

    private Task BroadcastMatchEnded(MatchEndedEffect effect, RoomActor actor)
    {
        return hub.Clients.Group(actor.RoomCode).MatchEnded(BuildStandings(effect.Players));
    }

    // rank is 1-based, players sorted best-score-first
    private static StandingDto[] BuildStandings(IReadOnlyList<Player> players) =>
        players
            .OrderByDescending(p => p.Score)
            .Select((player, rank) => new StandingDto(player.Id, player.Nick, player.Score, rank + 1))
            .ToArray();
}
