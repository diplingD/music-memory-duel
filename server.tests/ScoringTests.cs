using Server.Core.Game;

namespace Server.Tests;

public class ScoringTests
{
    private static Dictionary<string, bool> Solvers(params bool[] correctness)
        => correctness
            .Select((correct, i) => (Id: $"s{i}", Correct: correct))
            .ToDictionary(x => x.Id, x => x.Correct);

    // 9.1 — standard table (3+ players)

    [Fact]
    public void Confirmed_SomeButNotAllCorrect_CreatorGetsThree()
    {
        var (creator, _) = Scoring.Score(creatorConfirmed: true, Solvers(true, true, false, false));
        Assert.Equal(3, creator);
    }

    [Fact]
    public void Confirmed_AllCorrect_CreatorGetsZero()
    {
        var (creator, _) = Scoring.Score(creatorConfirmed: true, Solvers(true, true, true, true));
        Assert.Equal(0, creator);
    }

    [Fact]
    public void Confirmed_NoneCorrect_CreatorGetsZero()
    {
        var (creator, _) = Scoring.Score(creatorConfirmed: true, Solvers(false, false, false, false));
        Assert.Equal(0, creator);
    }

    [Fact]
    public void NotConfirmed_CreatorGetsMinusOne()
    {
        var (creator, _) = Scoring.Score(creatorConfirmed: false, Solvers(true, true, false, false));
        Assert.Equal(-1, creator);
    }

    [Fact]
    public void NotConfirmed_SolversStillScoreNormally()
    {
        var (_, solvers) = Scoring.Score(creatorConfirmed: false, Solvers(true, true, false, false));
        Assert.Equal(2, solvers["s0"]);
        Assert.Equal(2, solvers["s1"]);
        Assert.Equal(0, solvers["s2"]);
        Assert.Equal(0, solvers["s3"]);
    }

    [Fact]
    public void Solver_CorrectAndOnlyOneCorrect_GetsThree()
    {
        var (_, solvers) = Scoring.Score(creatorConfirmed: true, Solvers(true, false, false));
        Assert.Equal(3, solvers["s0"]);
    }

    [Fact]
    public void Solver_CorrectButNotAlone_GetsTwo()
    {
        var (_, solvers) = Scoring.Score(creatorConfirmed: true, Solvers(true, true, false));
        Assert.Equal(2, solvers["s0"]);
        Assert.Equal(2, solvers["s1"]);
    }

    [Fact]
    public void Solver_Wrong_GetsZero()
    {
        var (_, solvers) = Scoring.Score(creatorConfirmed: true, Solvers(false));
        Assert.Equal(0, solvers["s0"]);
    }

    // 9.1 — duel table (exactly one opponent)

    [Fact]
    public void Duel_NotConfirmed_OpponentCorrect()
    {
        var (creator, solvers) = Scoring.Score(creatorConfirmed: false, Solvers(true));
        Assert.Equal(-1, creator);
        Assert.Equal(3, solvers["s0"]);
    }

    [Fact]
    public void Duel_Confirmed_OpponentCorrect()
    {
        var (creator, solvers) = Scoring.Score(creatorConfirmed: true, Solvers(true));
        Assert.Equal(0, creator);
        Assert.Equal(1, solvers["s0"]);
    }

    [Fact]
    public void Duel_Confirmed_OpponentMissed()
    {
        var (creator, solvers) = Scoring.Score(creatorConfirmed: true, Solvers(false));
        Assert.Equal(1, creator);
        Assert.Equal(0, solvers["s0"]);
    }

    [Fact]
    public void Duel_NotConfirmed_OpponentMissed()
    {
        var (creator, solvers) = Scoring.Score(creatorConfirmed: false, Solvers(false));
        Assert.Equal(-1, creator);
        Assert.Equal(0, solvers["s0"]);
    }
}
