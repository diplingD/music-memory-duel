namespace Server.Core.Models;

// Answers holds BOTH the creator's confirmation replay and every solver's attempt
public sealed record Round
{
    public required Guid RoundId { get; init; }
    public required string CreatorId { get; init; }
    public required IReadOnlyList<NoteEvent> TargetNotes { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<NoteEvent>> Answers { get; init; }    // keyed by PlayerId
}
