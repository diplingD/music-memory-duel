using Server.Core.Models;

namespace Server.Core.Game;

public abstract record Effect;

public sealed record PlayerListChangedEffect(IReadOnlyList<Player> Players) : Effect;

public sealed record ErrorOccurred(string Code, string Message) : Effect;

public sealed record ScheduleEffect(DateTime WakeAtUtc, GameEvent EventToRaise) : Effect;

public sealed record MatchStartedEffect(DateTime ComposeDeadlineUtc, string ComposerId) : Effect;

public sealed record NotePlayedEffect(string ConnectionId, NoteEvent Note) : Effect;

public sealed record SolvingStartedEffect(Guid RoundId, DateTime SolveDeadlineUtc) : Effect;

public sealed record RoundEndedEffect(
    Guid RoundId,
    string ComposerId,
    bool ComposerConfirmed,
    IReadOnlyList<PlayerRoundResult> Results,
    IReadOnlyList<Player> Players,
    DateTime ResultDisplayDeadlineUtc) : Effect;

public sealed record MatchEndedEffect(IReadOnlyList<Player> Players) : Effect;
