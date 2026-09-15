using Server.Core.Models;

namespace Server.Core.Game;

public abstract record GameEvent;

public sealed record PlayerJoined(string PlayerId, string Nick, string ConnectionId, string Token) : GameEvent;

public sealed record PlayerDisconnected(string PlayerId) : GameEvent;

public sealed record PlayerReconnected(string PlayerId, string PlayerToken, string NewConnectionId) : GameEvent;

public sealed record MatchStartRequested : GameEvent;

public sealed record ComposeDeadlineReached(int RoundsPlayed) : GameEvent;

public sealed record SequenceSubmitted(string PlayerId, IReadOnlyList<NoteEvent> Notes) : GameEvent;

public sealed record NotePlayed(string PlayerId, string ConnectionId, NoteEvent Note) : GameEvent;

public sealed record AnswerSubmitted(string PlayerId, Guid RoundId, IReadOnlyList<NoteEvent> Notes) : GameEvent;

public sealed record SolveDeadlineReached(Guid RoundId) : GameEvent;

public sealed record ResultDisplayFinished : GameEvent;
