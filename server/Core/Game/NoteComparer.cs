using Server.Core.Models;

namespace Server.Core.Game;

public static class NoteComparer
{
    public static bool IsMatch(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt, double rhythmTolerance)
        => SameLength(target, attempt) && SamePitches(target, attempt) && RhythmWithinTolerance(target, attempt, rhythmTolerance);

    public static bool SameLength(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt)
        => target.Count == attempt.Count;

    public static bool SamePitches(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt)
        => target.Select(n => n.Pitch).SequenceEqual(attempt.Select(n => n.Pitch));

    // Compares intervals normalized to total sequence duration, so playing everything uniformly faster/slower doesn't matter
    public static bool RhythmWithinTolerance(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt, double tolerance)
    {
        if (target.Count != attempt.Count)
            return false;

        if (target.Count <= 1)
            return true; // no intervals to compare

        var targetIntervals = NormalizedIntervals(target);
        var attemptIntervals = NormalizedIntervals(attempt);

        for (var i = 0; i < targetIntervals.Length; i++)
        {
            if (targetIntervals[i] <= 0)
                continue; // degenerate original interval, nothing meaningful to compare

            var deviation = Math.Abs(attemptIntervals[i] - targetIntervals[i]) / targetIntervals[i];
            if (deviation > tolerance)
                return false;
        }

        return true;
    }

    // UI-only feedback ("6/9 notes") — how many notes from the start match before the first wrong pitch. Never used for scoring.
    public static double PrefixRatio(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt)
    {
        if (target.Count == 0)
            return 0;

        var matching = 0;
        for (var i = 0; i < Math.Min(target.Count, attempt.Count); i++)
        {
            if (target[i].Pitch != attempt[i].Pitch)
                break;
            matching++;
        }

        return (double)matching / target.Count;
    }

    private static double[] NormalizedIntervals(IReadOnlyList<NoteEvent> notes)
    {
        var totalMs = notes[^1].TMs;        // tMs of the last note
        if (totalMs <= 0)
            return new double[notes.Count - 1];

        return notes
            .Skip(1)
            .Select((n, i) => (n.TMs - notes[i].TMs) / (double)totalMs)
            .ToArray();     // tMs = [0, 200, 400, 600, 800]    =>  [(200-0)/800, (400-200)/800, (600-400)/800, (600-400)/800]  =   [0.25, 0.25, 0.25, 0.25]
    }
}
