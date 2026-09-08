using Server.Core.Game;
using Server.Core.Models;

namespace Server.Tests;

public class ReducerTests
{
    private static GameState LobbyWith(int playerCount)
    {
        var players = Enumerable.Range(0, playerCount)
            .Select(i => new Player { Id = $"p{i}", Nick = $"Player{i}", ConnectionId = $"c{i}" })
            .ToArray();

        return new GameState { Phase = Phase.Lobby, Players = players };
    }

    [Fact]
    public void MatchStartRequested_WithOnePlayer_StaysInLobbyAndReturnsError()
    {
        var state = LobbyWith(1);

        var (next, effects) = Reducer.Reduce(state, new MatchStartRequested(), DateTime.UtcNow);

        Assert.Equal(Phase.Lobby, next.Phase);
        Assert.Single(effects);
        Assert.IsType<ErrorOccurred>(effects[0]);
    }

    [Fact]
    public void MatchStartRequested_WithTwoPlayers_MovesToComposingAndSchedulesDeadline()
    {
        var state = LobbyWith(2);
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var (next, effects) = Reducer.Reduce(state, new MatchStartRequested(), now);
        var expectedDeadline = now.AddMilliseconds(GameConstants.ComposeMaxMs);

        Assert.Equal(Phase.Composing, next.Phase);
        Assert.Equal(2, effects.Count);

        var matchStarted = Assert.IsType<MatchStartedEffect>(effects[0]);
        Assert.Equal(expectedDeadline, matchStarted.ComposeDeadlineUtc);

        var schedule = Assert.IsType<ScheduleEffect>(effects[1]);
        Assert.Equal(expectedDeadline, schedule.WakeAtUtc);
        Assert.IsType<ComposeDeadlineReached>(schedule.EventToRaise);
    }

    [Fact]
    public void SequenceSubmitted_InComposing_MovesToSolvingAndStartsRound()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing };
        NoteEvent[] notes = [new("C4", 0), new("D4", 400)];

        var (next, effects) = Reducer.Reduce(state, new SequenceSubmitted("p0", notes), DateTime.UtcNow);

        Assert.Equal(Phase.Solving, next.Phase);
        Assert.NotNull(next.CurrentRound);
        Assert.Equal("p0", next.CurrentRound!.ComposerId);
        Assert.Equal(notes, next.CurrentRound.TargetNotes);
        Assert.Empty(next.CurrentRound.Answers);

        Assert.Equal(2, effects.Count);
        var solvingStarted = Assert.IsType<SolvingStartedEffect>(effects[0]);
        Assert.Equal(next.CurrentRound.RoundId, solvingStarted.RoundId);
        var schedule = Assert.IsType<ScheduleEffect>(effects[1]);
        Assert.IsType<SolveDeadlineReached>(schedule.EventToRaise);
    }

    [Fact]
    public void AnswerSubmitted_WhenEveryoneAnswered_ClosesRoundImmediately()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing };
        NoteEvent[] notes = [new("C4", 0), new("D4", 400)];
        var (afterSubmit, _) = Reducer.Reduce(state, new SequenceSubmitted("p0", notes), DateTime.UtcNow);
        var roundId = afterSubmit.CurrentRound!.RoundId;

        // composer confirms with the exact same sequence, then the only solver answers correctly
        var (afterComposer, composerEffects) = Reducer.Reduce(
            afterSubmit, new AnswerSubmitted("p0", roundId, notes), DateTime.UtcNow);
        Assert.Empty(composerEffects); // still waiting on the solver

        var (afterSolver, solverEffects) = Reducer.Reduce(
            afterComposer, new AnswerSubmitted("p1", roundId, notes), DateTime.UtcNow);

        Assert.Equal(Phase.RoundResult, afterSolver.Phase); // show results for a few seconds before continuing
        Assert.Null(afterSolver.CurrentRound);

        var roundEnded = Assert.IsType<RoundEndedEffect>(solverEffects[0]);
        Assert.True(roundEnded.ComposerConfirmed);
        Assert.Equal(0, afterSolver.Players.Single(p => p.Id == "p0").Score); // confirmed + all solved = trivial
        Assert.Equal(1, afterSolver.Players.Single(p => p.Id == "p1").Score); // duel, confirmed, correct = +1

        var schedule = Assert.IsType<ScheduleEffect>(solverEffects[1]);
        Assert.IsType<ResultDisplayFinished>(schedule.EventToRaise);
    }

    // Regression test: a round that closes early (everyone answered) leaves its originally
    // scheduled SolveDeadlineReached still pending — the Scheduler has no cancellation. That
    // stale event must not be able to close a LATER round that's still legitimately in Solving.
    [Fact]
    public void SolveDeadlineReached_WithStaleRoundId_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 };
        NoteEvent[] notes = [new("C4", 0), new("D4", 400)];

        var (afterSubmit1, _) = Reducer.Reduce(state, new SequenceSubmitted("p0", notes), DateTime.UtcNow);
        var staleRoundId = afterSubmit1.CurrentRound!.RoundId;

        var (afterP0, _) = Reducer.Reduce(afterSubmit1, new AnswerSubmitted("p0", staleRoundId, notes), DateTime.UtcNow);
        var (afterClosed1, _) = Reducer.Reduce(afterP0, new AnswerSubmitted("p1", staleRoundId, notes), DateTime.UtcNow);

        var (afterComposing2, _) = Reducer.Reduce(afterClosed1, new ResultDisplayFinished(), DateTime.UtcNow);
        var (afterSubmit2, _) = Reducer.Reduce(afterComposing2, new SequenceSubmitted("p1", notes), DateTime.UtcNow);
        var round2Id = afterSubmit2.CurrentRound!.RoundId;
        Assert.NotEqual(staleRoundId, round2Id);

        // the stale SolveDeadlineReached from round 1 arrives while round 2 is legitimately Solving
        var (afterStale, staleEffects) = Reducer.Reduce(
            afterSubmit2, new SolveDeadlineReached(staleRoundId), DateTime.UtcNow);

        Assert.Empty(staleEffects);
        Assert.Equal(Phase.Solving, afterStale.Phase);
        Assert.Equal(round2Id, afterStale.CurrentRound!.RoundId);
    }

    [Fact]
    public void ResultDisplayFinished_BeforeLastRound_ReturnsToComposing()
    {
        var state = LobbyWith(2) with { Phase = Phase.RoundResult, TotalRounds = 6, RoundsPlayed = 1 };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var (next, effects) = Reducer.Reduce(state, new ResultDisplayFinished(), now);
        var expectedDeadline = now.AddMilliseconds(GameConstants.ComposeMaxMs);

        Assert.Equal(Phase.Composing, next.Phase);
        var matchStarted = Assert.IsType<MatchStartedEffect>(effects[0]);
        Assert.Equal(expectedDeadline, matchStarted.ComposeDeadlineUtc);
    }

    [Fact]
    public void ResultDisplayFinished_OnLastRound_EntersMatchOver()
    {
        var state = LobbyWith(2) with { Phase = Phase.RoundResult, TotalRounds = 6, RoundsPlayed = 6 };

        var (next, effects) = Reducer.Reduce(state, new ResultDisplayFinished(), DateTime.UtcNow);

        Assert.Equal(Phase.MatchOver, next.Phase);
        var matchEnded = Assert.IsType<MatchEndedEffect>(Assert.Single(effects));
        Assert.Equal(2, matchEnded.Players.Count);
    }

    [Fact]
    public void MatchStartRequested_SetsTotalRoundsFromPlayerCount()
    {
        var state = LobbyWith(3);

        var (next, _) = Reducer.Reduce(state, new MatchStartRequested(), DateTime.UtcNow);

        Assert.Equal(3 * GameConstants.RoundsPerPlayer, next.TotalRounds);
    }

    [Fact]
    public void SequenceSubmitted_OutsideComposing_IsIgnored()
    {
        var state = LobbyWith(2);

        var (next, effects) = Reducer.Reduce(
            state, new SequenceSubmitted("p0", [new NoteEvent("C4", 0)]), DateTime.UtcNow);

        Assert.Equal(Phase.Lobby, next.Phase);
        Assert.Empty(effects);
    }

    [Fact]
    public void PlayerJoined_InLobby_AddsPlayerAndBroadcastsList()
    {
        var state = LobbyWith(1);

        var (next, effects) = Reducer.Reduce(
            state, new PlayerJoined("p1", "Ana", "conn-1"), DateTime.UtcNow);

        Assert.Equal(2, next.Players.Count);
        Assert.Equal("Ana", next.Players[1].Nick);

        var effect = Assert.IsType<PlayerListChangedEffect>(Assert.Single(effects));
        Assert.Equal(2, effect.Players.Count);
    }
}
