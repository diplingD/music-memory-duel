using Server.Core.Models;

namespace Server.Core.Game;

public abstract record GameEvent;

public sealed record PlayerJoined(string PlayerId, string Nick, string ConnectionId) : GameEvent;

public sealed record MatchStartRequested : GameEvent;

public sealed record ComposeDeadlineReached : GameEvent;

public sealed record SequenceSubmitted(string PlayerId, IReadOnlyList<NoteEvent> Notes) : GameEvent;

public sealed record NotePlayed(string PlayerId, string ConnectionId, NoteEvent Note) : GameEvent;

public sealed record AnswerSubmitted(string PlayerId, Guid RoundId, IReadOnlyList<NoteEvent> Notes) : GameEvent;

public sealed record SolveDeadlineReached : GameEvent;

public sealed record ResultDisplayFinished : GameEvent;
