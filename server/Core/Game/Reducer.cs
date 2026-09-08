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

            case SolveDeadlineReached:
                return HandleSolveDeadlineReached(state, utcNow);

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

        return EnterComposing(state, utcNow);
    }

    // Shared by the very first round (from Lobby) and every round after the first
    // (from RoundResult) — same "open a fresh Composing window" effects either way.
    private static (GameState, IReadOnlyList<Effect>) EnterComposing(GameState state, DateTime utcNow)
    {
        var deadline = utcNow.AddMilliseconds(GameConstants.ComposeMaxMs);
        var updatedState = state with { Phase = Phase.Composing };
        List<Effect> effects =
        [
            new MatchStartedEffect(deadline),
            new ScheduleEffect(deadline, new ComposeDeadlineReached()),
        ];

        return (updatedState, effects);
    }

    // Live broadcast while the creator is composing — doesn't change state, just relays
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

        // first note is always tMs = 0, so the last note's tMs is how long the creator actually took.
        var creatorDurationMs = evt.Notes.Count > 0 ? evt.Notes.Max(n => n.TMs) : 0;
        var solveMs = GameConstants.SolveMaxMs;     // temporary for dev phase
        //var solveMs = Math.Clamp(
        //    creatorDurationMs * GameConstants.SolveMultiplier,
        //    GameConstants.SolveMinMs,
        //    GameConstants.SolveMaxMs);
        var solveDeadline = utcNow.AddMilliseconds(solveMs);

        var round = new Round
        {
            RoundId = Guid.NewGuid(),
            CreatorId = evt.PlayerId,
            TargetNotes = evt.Notes,
            Answers = new Dictionary<string, IReadOnlyList<NoteEvent>>(),
        };

        var updatedState = state with { Phase = Phase.Solving, CurrentRound = round };
        List<Effect> effects =
        [
            new SolvingStartedEffect(round.RoundId, solveDeadline),
            new ScheduleEffect(solveDeadline, new SolveDeadlineReached()),
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

        // creator + every solver have all answered -> close the round now, don't wait for the deadline
        if (updatedAnswers.Count == state.Players.Count)
            return CloseRound(updatedState, utcNow);

        return (updatedState, []);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleSolveDeadlineReached(GameState state, DateTime utcNow)
    {
        if (state.Phase != Phase.Solving || state.CurrentRound is null)
            return (state, []);

        return CloseRound(state, utcNow);
    }

    private static (GameState, IReadOnlyList<Effect>) HandleResultDisplayFinished(GameState state, DateTime utcNow)
    {
        if (state.Phase != Phase.RoundResult)
            return (state, []);

        return EnterComposing(state, utcNow);
    }

    // Shared by both ways a round can end: everyone answered early, or the deadline hit.
    // Missing answers (creator or solver) are simply treated as "didn't submit" — timeout.
    private static (GameState, IReadOnlyList<Effect>) CloseRound(GameState state, DateTime utcNow)
    {
        var round = state.CurrentRound!;

        var creatorAttempt = round.Answers.GetValueOrDefault(round.CreatorId);
        var creatorConfirmed = creatorAttempt is not null
            && NoteComparer.IsMatch(round.TargetNotes, creatorAttempt, GameConstants.RhythmTolerance);

        var solverIds = state.Players.Select(p => p.Id).Where(id => id != round.CreatorId).ToArray();
        var solverCorrectness = solverIds.ToDictionary(
            id => id,
            id => round.Answers.TryGetValue(id, out var attempt)
                && NoteComparer.IsMatch(round.TargetNotes, attempt, GameConstants.RhythmTolerance));

        var (creatorPoints, solverPoints) = Scoring.Score(creatorConfirmed, solverCorrectness);

        var results = new List<PlayerRoundResult>
        {
            BuildResult(round, round.CreatorId, creatorAttempt, creatorConfirmed, creatorPoints, GameConstants.RhythmTolerance),
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

        // Known temporary gap: no creator rotation yet (Korak 4) — ResultDisplayFinished
        // always re-enters Composing with the same creator.
        var resultDisplayDeadline = utcNow.AddMilliseconds(GameConstants.ResultDisplayMs);
        var updatedState = state with { Phase = Phase.RoundResult, Players = updatedPlayers, CurrentRound = null };
        List<Effect> effects =
        [
            new RoundEndedEffect(round.RoundId, round.CreatorId, creatorConfirmed, results, updatedPlayers, resultDisplayDeadline),
            new ScheduleEffect(resultDisplayDeadline, new ResultDisplayFinished()),
        ];

        return (updatedState, effects);
    }

    private static PlayerRoundResult BuildResult(
        Round round, string playerId, IReadOnlyList<NoteEvent>? attempt, bool correct, int pointsDelta, double rhythmTolerance)
    {
        var submitted = attempt is not null;
        var reason = !submitted ? "timeout"
            : !NoteComparer.SameLength(round.TargetNotes, attempt!) ? "wrong_length"
            : !NoteComparer.SamePitches(round.TargetNotes, attempt!) ? "wrong_pitch"
            : !correct ? "wrong_rhythm"
            : "correct";
        var prefixRatio = submitted ? NoteComparer.PrefixRatio(round.TargetNotes, attempt!) : 0;

        return new PlayerRoundResult(playerId, submitted, correct, prefixRatio, pointsDelta, reason);
    }
}
