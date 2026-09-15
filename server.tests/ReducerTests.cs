using Server.Core.Game;
using Server.Core.Models;

namespace Server.Tests;

public class ReducerTests
{
    private static GameState LobbyWith(int playerCount)
    {
        var players = Enumerable.Range(0, playerCount)
            .Select(i => new Player { Id = $"p{i}", Nick = $"Player{i}", ConnectionId = $"c{i}", Token = $"t{i}" })
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
            state, new PlayerJoined("p1", "Ana", "conn-1", "token-1"), DateTime.UtcNow);

        Assert.Equal(2, next.Players.Count);
        Assert.Equal("Ana", next.Players[1].Nick);

        var effect = Assert.IsType<PlayerListChangedEffect>(Assert.Single(effects));
        Assert.Equal(2, effect.Players.Count);
    }

    [Fact]
    public void PlayerDisconnected_NonComposer_JustMarksNotConnected()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 }; // ComposerIndex 0 -> p0

        var (next, effects) = Reducer.Reduce(state, new PlayerDisconnected("p1"), DateTime.UtcNow);

        Assert.Equal(Phase.Composing, next.Phase);
        Assert.False(next.Players.Single(p => p.Id == "p1").IsConnected);
        Assert.True(next.Players.Single(p => p.Id == "p0").IsConnected);
        var effect = Assert.IsType<PlayerListChangedEffect>(Assert.Single(effects));
        Assert.False(effect.Players.Single(p => p.Id == "p1").IsConnected);
    }

    [Fact]
    public void PlayerDisconnected_AlreadyDisconnected_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 };
        var (afterFirst, _) = Reducer.Reduce(state, new PlayerDisconnected("p1"), DateTime.UtcNow);

        var (next, effects) = Reducer.Reduce(afterFirst, new PlayerDisconnected("p1"), DateTime.UtcNow);

        Assert.Empty(effects);
        Assert.Same(afterFirst, next);
    }

    [Fact]
    public void PlayerDisconnected_UnknownPlayer_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 };

        var (next, effects) = Reducer.Reduce(state, new PlayerDisconnected("ghost"), DateTime.UtcNow);

        Assert.Empty(effects);
        Assert.Same(state, next);
    }

    [Fact]
    public void PlayerDisconnected_ComposerDuringComposing_VoidsRoundAndAdvancesToNextComposer()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10, ComposerIndex = 0 };

        var (next, effects) = Reducer.Reduce(state, new PlayerDisconnected("p0"), DateTime.UtcNow);

        Assert.Equal(Phase.Composing, next.Phase);
        Assert.Equal(1, next.ComposerIndex);
        Assert.Equal(1, next.RoundsPlayed);

        Assert.Equal(3, effects.Count);
        Assert.IsType<PlayerListChangedEffect>(effects[0]);
        var matchStarted = Assert.IsType<MatchStartedEffect>(effects[1]);
        Assert.Equal("p1", matchStarted.ComposerId); // rotated to the next player
        Assert.IsType<ScheduleEffect>(effects[2]);
    }

    [Fact]
    public void PlayerDisconnected_ComposerOnLastRound_EntersMatchOver()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 6, RoundsPlayed = 5, ComposerIndex = 0 };

        var (next, effects) = Reducer.Reduce(state, new PlayerDisconnected("p0"), DateTime.UtcNow);

        Assert.Equal(Phase.MatchOver, next.Phase);
        Assert.Equal(6, next.RoundsPlayed);
        Assert.Equal(2, effects.Count);
        Assert.IsType<PlayerListChangedEffect>(effects[0]);
        Assert.IsType<MatchEndedEffect>(effects[1]);
    }

    [Fact]
    public void PlayerReconnected_WithValidToken_MarksConnectedAndUpdatesConnectionIdAndSendsSnapshot()
    {
        var state = LobbyWith(2) with
        {
            Phase = Phase.Composing,
            TotalRounds = 10,
            CurrentPhaseDeadlineUtc = DateTime.UtcNow.AddSeconds(10),
        }; // ComposerIndex 0 -> p0
        var (afterDisconnect, _) = Reducer.Reduce(state, new PlayerDisconnected("p1"), DateTime.UtcNow);

        var (next, effects) = Reducer.Reduce(
            afterDisconnect, new PlayerReconnected("p1", "t1", "new-conn"), DateTime.UtcNow);

        var reconnected = next.Players.Single(p => p.Id == "p1");
        Assert.True(reconnected.IsConnected);
        Assert.Equal("new-conn", reconnected.ConnectionId);

        Assert.Equal(2, effects.Count);
        Assert.IsType<PlayerListChangedEffect>(effects[0]);
        var snapshot = Assert.IsType<RoomSnapshotEffect>(effects[1]);
        Assert.Equal("new-conn", snapshot.ConnectionId);
        Assert.Equal(Phase.Composing, snapshot.Phase);
        Assert.Equal("p0", snapshot.ComposerId);
        Assert.Null(snapshot.RoundId); // no Round object exists yet during Composing
        Assert.NotNull(snapshot.CurrentPhaseDeadlineUtc);
    }

    [Fact]
    public void PlayerReconnected_DuringSolving_SnapshotIncludesRoundId()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 };
        NoteEvent[] notes = [new("C4", 0), new("D4", 400)];
        var (afterSubmit, _) = Reducer.Reduce(state, new SequenceSubmitted("p0", notes), DateTime.UtcNow);
        var roundId = afterSubmit.CurrentRound!.RoundId;

        var (_, effects) = Reducer.Reduce(afterSubmit, new PlayerReconnected("p1", "t1", "new-conn"), DateTime.UtcNow);

        var snapshot = Assert.IsType<RoomSnapshotEffect>(effects[1]);
        Assert.Equal(Phase.Solving, snapshot.Phase);
        Assert.Equal(roundId, snapshot.RoundId);
        Assert.Equal("p0", snapshot.ComposerId);
    }

    [Fact]
    public void PlayerReconnected_WithWrongToken_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10 };

        var (next, effects) = Reducer.Reduce(state, new PlayerReconnected("p1", "wrong-token", "new-conn"), DateTime.UtcNow);

        Assert.Empty(effects);
        Assert.Same(state, next);
    }

    // Regression test: the composer's time runs out with nothing submitted. Before this was
    // wired up, ComposeDeadlineReached had no case in the switch — the room got stuck in
    // Composing forever (found by the user testing reconnect, but it's a separate, older gap).
    [Fact]
    public void ComposeDeadlineReached_NothingSubmitted_VoidsRoundAndAdvancesToNextComposer()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10, ComposerIndex = 0, RoundsPlayed = 0 };

        var (next, effects) = Reducer.Reduce(state, new ComposeDeadlineReached(0), DateTime.UtcNow);

        Assert.Equal(Phase.Composing, next.Phase);
        Assert.Equal(1, next.ComposerIndex);
        Assert.Equal(1, next.RoundsPlayed);

        Assert.Equal(2, effects.Count);
        var matchStarted = Assert.IsType<MatchStartedEffect>(effects[0]);
        Assert.Equal("p1", matchStarted.ComposerId); // rotated to the next player
        Assert.IsType<ScheduleEffect>(effects[1]);
    }

    [Fact]
    public void ComposeDeadlineReached_OnLastRound_EntersMatchOver()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 6, RoundsPlayed = 5, ComposerIndex = 0 };

        var (next, effects) = Reducer.Reduce(state, new ComposeDeadlineReached(5), DateTime.UtcNow);

        Assert.Equal(Phase.MatchOver, next.Phase);
        Assert.Equal(6, next.RoundsPlayed);
        Assert.IsType<MatchEndedEffect>(Assert.Single(effects));
    }

    // Same stale-event problem as SolveDeadlineReached: the composer submits in time, the round
    // moves on (RoundsPlayed increments), but the originally-scheduled ComposeDeadlineReached
    // from THAT round is still pending and must not void a later, unrelated Composing session.
    [Fact]
    public void ComposeDeadlineReached_WithStaleRoundsPlayed_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Composing, TotalRounds = 10, ComposerIndex = 0, RoundsPlayed = 3 };

        var (next, effects) = Reducer.Reduce(state, new ComposeDeadlineReached(2), DateTime.UtcNow);

        Assert.Empty(effects);
        Assert.Same(state, next);
    }

    [Fact]
    public void ComposeDeadlineReached_OutsideComposing_IsIgnored()
    {
        var state = LobbyWith(2) with { Phase = Phase.Solving, TotalRounds = 10, RoundsPlayed = 0 };

        var (next, effects) = Reducer.Reduce(state, new ComposeDeadlineReached(0), DateTime.UtcNow);

        Assert.Empty(effects);
        Assert.Same(state, next);
    }
}
