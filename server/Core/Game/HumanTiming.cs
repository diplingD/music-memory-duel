using Server.Core.Models;

namespace Server.Core.Game;

// Defense against a replay attack: a client that just echoes back the exact values it received live (via NotePlayed)
// has zero human jitter — something a real player physically can't produce (SPEC 8.1).
public static class HumanTiming
{
    public static bool IsHumanTiming(IReadOnlyList<NoteEvent> target, IReadOnlyList<NoteEvent> attempt)
    {
        // 1) check if someone played melody that is impossible to repeat
        for (var i = 1; i < attempt.Count; i++)
            if (attempt[i].TMs - attempt[i - 1].TMs < GameConstants.MinNoteIntervalMs)
                return false;

        // 2) must not be a machine-exact copy of the target timing
        if (attempt.Count >= 3 && attempt.Count == target.Count)
        {
            double totalDeviationMs = 0;
            for (var i = 0; i < attempt.Count; i++)
                totalDeviationMs += Math.Abs(attempt[i].TMs - target[i].TMs);

            var averageDeviationMs = totalDeviationMs / attempt.Count;
            if (averageDeviationMs < 5.0) return false; // < 5ms average deviation = bot/replay
        }

        return true;
    }
}
