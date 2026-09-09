using Server.Core.Game;
using Server.Core.Models;

namespace Server.Validators;

public static class SubmissionValidator
{
    public static bool IsValid(IReadOnlyCollection<NoteEvent> notes)
    {
        if (notes.Count < GameConstants.MinNotes || notes.Count > GameConstants.MaxNotes)
            return false;

        var lastTMs = int.MinValue;
        foreach (var note in notes)
        {
            if (note.TMs < lastTMs)
                return false;

            lastTMs = note.TMs;
        }

        return true;
    }
}
