namespace Server.Core.Game;

public static class Scoring
{
    // solverCorrectness: playerId -> did they reproduce the sequence correctly.
    public static (int ComposerPoints, IReadOnlyDictionary<string, int> SolverPoints) Score(
        bool composerConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        return solverCorrectness.Count == 1
            ? ScoreDuel(composerConfirmed, solverCorrectness)
            : ScoreStandard(composerConfirmed, solverCorrectness);
    }

    private static (int, IReadOnlyDictionary<string, int>) ScoreStandard(
        bool composerConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        var correctCount = solverCorrectness.Values.Count(correct => correct);
        var totalSolvers = solverCorrectness.Count;

        var composerPoints = !composerConfirmed ? -1
            : correctCount == 0 || correctCount == totalSolvers ? 0  // unsolvable or trivial
            : 3;                                                      // some but not all

        // Solvers score the same way regardless of whether the composer confirmed — SPEC 4.4:
        // "kada kreator ne potvrdi, solveri normalno boduju".
        var solverPoints = solverCorrectness.ToDictionary(
            kv => kv.Key,
            kv => !kv.Value ? 0 : correctCount == 1 ? 3 : 2);

        return (composerPoints, solverPoints);
    }

    private static (int, IReadOnlyDictionary<string, int>) ScoreDuel(
        bool composerConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        var (opponentId, opponentCorrect) = solverCorrectness.Single();

        int composerPoints;
        int opponentPoints;

        if (!composerConfirmed)
        {
            composerPoints = -1;
            opponentPoints = opponentCorrect ? 3 : 0;
        }
        else
        {
            composerPoints = opponentCorrect ? 0 : 1;
            opponentPoints = opponentCorrect ? 1 : 0;
        }

        return (composerPoints, new Dictionary<string, int> { [opponentId] = opponentPoints });
    }
}
