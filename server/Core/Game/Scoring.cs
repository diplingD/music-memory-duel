namespace Server.Core.Game;

public static class Scoring
{
    // solverCorrectness: playerId -> did they reproduce the sequence correctly.
    public static (int CreatorPoints, IReadOnlyDictionary<string, int> SolverPoints) Score(
        bool creatorConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        return solverCorrectness.Count == 1
            ? ScoreDuel(creatorConfirmed, solverCorrectness)
            : ScoreStandard(creatorConfirmed, solverCorrectness);
    }

    private static (int, IReadOnlyDictionary<string, int>) ScoreStandard(
        bool creatorConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        var correctCount = solverCorrectness.Values.Count(correct => correct);
        var totalSolvers = solverCorrectness.Count;

        var creatorPoints = !creatorConfirmed ? -1
            : correctCount == 0 || correctCount == totalSolvers ? 0  // unsolvable or trivial
            : 3;                                                      // some but not all

        // Solvers score the same way regardless of whether the creator confirmed — SPEC 4.4:
        // "kada kreator ne potvrdi, solveri normalno boduju".
        var solverPoints = solverCorrectness.ToDictionary(
            kv => kv.Key,
            kv => !kv.Value ? 0 : correctCount == 1 ? 3 : 2);

        return (creatorPoints, solverPoints);
    }

    private static (int, IReadOnlyDictionary<string, int>) ScoreDuel(
        bool creatorConfirmed, IReadOnlyDictionary<string, bool> solverCorrectness)
    {
        var (opponentId, opponentCorrect) = solverCorrectness.Single();

        int creatorPoints;
        int opponentPoints;

        if (!creatorConfirmed)
        {
            creatorPoints = -1;
            opponentPoints = opponentCorrect ? 3 : 0;
        }
        else
        {
            creatorPoints = opponentCorrect ? 0 : 1;
            opponentPoints = opponentCorrect ? 1 : 0;
        }

        return (creatorPoints, new Dictionary<string, int> { [opponentId] = opponentPoints });
    }
}
