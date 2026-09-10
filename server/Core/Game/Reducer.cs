using Server.Core.Models;

namespace Server.Core.Game;

public static class Reducer
{
    public static (GameState State, IReadOnlyList<Effect> Effects) Reduce(
        GameState state, GameEvent evt, DateTime utcNow)
    {
        switch (evt)
        {
            case PlayerJoined playerJoined:
                return HandlePlayerJoined(state, playerJoined);

            case MatchStartRequested:
                return HandleMatchStartRequested(state, utcNow);

            case NotePlayed notePlayed:
                return HandleNotePlayed(state, notePlayed);

            case SequenceSubmitted sequenceSubmitted:
                return HandleSequenceSubmitted(state, sequenceSubmitted, utcNow);

            case AnswerSubmitted answerSubmitted:
                return HandleAnswerSubmitted(state, answerSubmitted, utcNow);

            case SolveDeadlineReached solveDeadlineReached:
                return HandleSolveDeadlineReached(state, solveDeadlineReached, utcNow);

            case ResultDisplayFinished:
                return HandleResultDisplayFinished(state, utcNow);

            default:
                return (state, []);
        }
    }

    private static (GameState, IReadOnlyList<Effect>) HandlePlayerJoined(GameState state, PlayerJoined evt)
    {
        if (state.Phase != Phase.Lobby)
            return (state, []);

        var player = new Player { Id = evt.PlayerId, Nick = evt.Nick, ConnectionId = evt.ConnectionId };
        IReadOnlyList<Player> updatedPlayers = [.. state.Players, player];

        var updatedState = state with { Players = updatedPlayers };     // state - but with updated 'Players' field
        var effects = new List<Effect> { new PlayerListChangedEffect(updatedPlayers) };

        return (updatedState, effects);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleMatchStartRequested(GameState state, DateTime utcNow)
    {
        if (state.Phase != Phase.Lobby)
            return (state, []);

        if (state.Players.Count < GameConstants.MinPlayers)
            return (state, [new ErrorOccurred("not_enough_players", "You need at least 2 players to start.")]);

        var totalRounds = state.Players.Count * GameConstants.RoundsPerPlayer;
        return EnterComposing(state with { TotalRounds = totalRounds }, utcNow);
    }

    // Shared by the very first round (from Lobby) and every round after the RoundResult phase
    private static (GameState, IReadOnlyList<Effect>) EnterComposing(GameState state, DateTime utcNow)
    {
        var composerId = CurrentComposerId(state);
        var deadline = utcNow.AddMilliseconds(GameConstants.ComposeMaxMs);
        var updatedState = state with { Phase = Phase.Composing };
        List<Effect> effects =
        [
            new MatchStartedEffect(deadline, composerId),
            new ScheduleEffect(deadline, new ComposeDeadlineReached()),
        ];

        return (updatedState, effects);
    }

    // Round-robin by join order — Players is fixed for the whole match (no joins after Lobby).
    // Moduo here is not necessary, since we already do it in HandleResultDisplayFinished below
    private static string CurrentComposerId(GameState state) =>
        state.Players[state.ComposerIndex % state.Players.Count].Id;

    // Live broadcast while the composer is composing — doesn't change state, just relays
    // the note to everyone else in the room so they can hear/see it as it's played.
    private static (GameState, IReadOnlyList<Effect>) HandleNotePlayed(GameState state, NotePlayed evt)
    {
        if (state.Phase != Phase.Composing)
            return (state, []);

        List<Effect> effects = [new NotePlayedEffect(evt.ConnectionId, evt.Note)];

        return (state, effects);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleSequenceSubmitted(
        GameState state, SequenceSubmitted evt, DateTime utcNow)
    {
        if (state.Phase != Phase.Composing)
            return (state, []);

        var composerId = CurrentComposerId(state);
        if (evt.PlayerId != composerId)
            return (state, [new ErrorOccurred("not_your_turn", "Only the current composer can submit the sequence.")]);

        // first note is always tMs = 0, so the last note's tMs is how long the composer actually took.
        var composerDurationMs = evt.Notes.Count > 0 ? evt.Notes.Max(n => n.TMs) : 0;
        var solveMs = GameConstants.SolveMaxMs;     // temporary for dev phase
        //var solveMs = Math.Clamp(
        //    composerDurationMs * GameConstants.SolveMultiplier,
        //    GameConstants.SolveMinMs,
        //    GameConstants.SolveMaxMs);
        var solveDeadline = utcNow.AddMilliseconds(solveMs);

        var round = new Round
        {
            RoundId = Guid.NewGuid(),
            ComposerId = composerId,
            TargetNotes = evt.Notes,
            Answers = new Dictionary<string, IReadOnlyList<NoteEvent>>(),
        };

        var updatedState = state with { Phase = Phase.Solving, CurrentRound = round };
        List<Effect> effects =
        [
            new SolvingStartedEffect(round.RoundId, solveDeadline),
            new ScheduleEffect(solveDeadline, new SolveDeadlineReached(round.RoundId)),
        ];

        return (updatedState, effects);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleAnswerSubmitted(
        GameState state, AnswerSubmitted evt, DateTime utcNow)
    {
        if (state.Phase != Phase.Solving || state.CurrentRound is null)
            return (state, []);

        var round = state.CurrentRound;

        if (evt.RoundId != round.RoundId)
            return (state, []); // stale submission from a round that already closed

        if (round.Answers.ContainsKey(evt.PlayerId))
            return (state, []); // ignore duplicate submit

        var updatedAnswers = new Dictionary<string, IReadOnlyList<NoteEvent>>(round.Answers)
        {
            [evt.PlayerId] = evt.Notes,
        };
        var updatedState = state with { CurrentRound = round with { Answers = updatedAnswers } };

        // composer + every solver have all answered -> close the round now, don't wait for the deadline
        if (updatedAnswers.Count == state.Players.Count)
            return CloseRound(updatedState, utcNow);

        return (updatedState, []);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleSolveDeadlineReached(
        GameState state, SolveDeadlineReached evt, DateTime utcNow)
    {
        if (state.Phase != Phase.Solving || state.CurrentRound is null)
            return (state, []);

        if (evt.RoundId != state.CurrentRound.RoundId)
            return (state, []); // stale deadline from a round that already closed early

        return CloseRound(state, utcNow);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleResultDisplayFinished(GameState state, DateTime utcNow)
    {
        if (state.Phase != Phase.RoundResult)
            return (state, []);

        if (state.RoundsPlayed >= state.TotalRounds)
            return EnterMatchOver(state);

        var nextIndex = (state.ComposerIndex + 1) % state.Players.Count;
        var updatedState = state with { ComposerIndex = nextIndex };
        return EnterComposing(updatedState, utcNow);
    }

    private static (GameState, IReadOnlyList<Effect>) EnterMatchOver(GameState state)
    {
        var updatedState = state with { Phase = Phase.MatchOver };
        List<Effect> effects = [new MatchEndedEffect(state.Players)];

        return (updatedState, effects);
    }

    // Shared by both ways a round can end: everyone answered early, or the deadline hit.
    // Missing answers (composer or solver) are simply treated as "didn't submit" — timeout.
    private static (GameState, IReadOnlyList<Effect>) CloseRound(GameState state, DateTime utcNow)
    {
        var round = state.CurrentRound!;

        var composerAttempt = round.Answers.GetValueOrDefault(round.ComposerId);
        var composerConfirmed = composerAttempt is not null
            && HumanTiming.IsHumanTiming(round.TargetNotes, composerAttempt)
            && NoteComparer.IsMatch(round.TargetNotes, composerAttempt, GameConstants.RhythmTolerance);

        var solverIds = state.Players.Select(p => p.Id).Where(id => id != round.ComposerId).ToArray();
        var solverCorrectness = solverIds.ToDictionary(
            id => id,
            id => round.Answers.TryGetValue(id, out var attempt)
                && HumanTiming.IsHumanTiming(round.TargetNotes, attempt)
                && NoteComparer.IsMatch(round.TargetNotes, attempt, GameConstants.RhythmTolerance));

        var (composerPoints, solverPoints) = Scoring.Score(composerConfirmed, solverCorrectness);

        var results = new List<PlayerRoundResult>
        {
            BuildResult(round, round.ComposerId, composerAttempt, composerConfirmed, composerPoints, GameConstants.RhythmTolerance),
        };
        foreach (var id in solverIds)
        {
            var attempt = round.Answers.GetValueOrDefault(id);
            results.Add(BuildResult(round, id, attempt, solverCorrectness[id], solverPoints[id], GameConstants.RhythmTolerance));
        }

        var pointsById = results.ToDictionary(r => r.PlayerId, r => r.PointsDelta);
        var updatedPlayers = state.Players
            .Select(p => new Player
            {
                Id = p.Id,
                Nick = p.Nick,
                ConnectionId = p.ConnectionId,
                Score = p.Score + pointsById[p.Id],
            })
            .ToList();

        var resultDisplayDeadline = utcNow.AddMilliseconds(GameConstants.ResultDisplayMs);
        var updatedState = state with
        {
            Phase = Phase.RoundResult,
            Players = updatedPlayers,
            CurrentRound = null,
            RoundsPlayed = state.RoundsPlayed + 1,
        };
        List<Effect> effects =
        [
            new RoundEndedEffect(round.RoundId, round.ComposerId, composerConfirmed, results, updatedPlayers, resultDisplayDeadline),
            new ScheduleEffect(resultDisplayDeadline, new ResultDisplayFinished()),
        ];

        return (updatedState, effects);
    }

    private static PlayerRoundResult BuildResult(
        Round round, string playerId, IReadOnlyList<NoteEvent>? attempt, bool correct, int pointsDelta, double rhythmTolerance)
    {
        var submitted = attempt is not null;
        var reason = !submitted ? "timeout"
            : !HumanTiming.IsHumanTiming(round.TargetNotes, attempt!) ? "implausible_timing"
            : !NoteComparer.SameLength(round.TargetNotes, attempt!) ? "wrong_length"
            : !NoteComparer.SamePitches(round.TargetNotes, attempt!) ? "wrong_pitch"
            : !correct ? "wrong_rhythm"
            : "correct";
        var prefixRatio = submitted ? NoteComparer.PrefixRatio(round.TargetNotes, attempt!) : 0;

        return new PlayerRoundResult(playerId, submitted, correct, prefixRatio, pointsDelta, reason);
    }
}
