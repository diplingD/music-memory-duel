using Server.Core.Game;
using Server.Core.Models;

namespace Server.Tests;

public class NoteComparerTests
{
    private const double Tolerance = GameConstants.RhythmTolerance; // 0.30

    // pitch

    [Fact]
    public void SamePitchesAndTiming_IsMatch()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 300), new("E4", 700)];

        Assert.True(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void SwappedPitchOrder_IsNotMatch()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300)];
        NoteEvent[] attempt = [new("D4", 0), new("C4", 300)];

        Assert.False(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void DifferentLength_IsNotMatch()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 300)];

        Assert.False(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    // rhythm

    [Fact]
    public void SameMotif25PercentFaster_IsMatch()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 225), new("E4", 525)]; // uniformly x0.75

        Assert.True(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void SameMotif25PercentSlower_IsMatch()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 375), new("E4", 875)]; // uniformly x1.25

        Assert.True(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void ReorderedRhythmPattern_IsNotMatch()
    {
        // "short-short-long" vs "short-long-short" — same pitches, same total duration, different shape
        NoteEvent[] target = [new("C4", 0), new("D4", 200), new("E4", 400), new("F4", 1000)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 200), new("E4", 800), new("F4", 1000)];

        Assert.False(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void FifteenPercentJitterPerInterval_IsWithinTolerance()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 200), new("E4", 400), new("F4", 600), new("G4", 800)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 230), new("E4", 400), new("F4", 630), new("G4", 800)];

        Assert.True(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void LargeJitterOnOneInterval_ExceedsTolerance()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 200), new("E4", 400), new("F4", 600), new("G4", 800)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 320), new("E4", 520), new("F4", 720), new("G4", 920)];

        Assert.False(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    [Fact]
    public void SingleNote_HasNoIntervals_RhythmTriviallyMatches()
    {
        NoteEvent[] target = [new("C4", 0)];
        NoteEvent[] attempt = [new("C4", 0)];

        Assert.True(NoteComparer.IsMatch(target, attempt, Tolerance));
    }

    // prefix ratio (UI feedback only, never used for scoring)

    [Fact]
    public void PrefixRatio_SixOfNineCorrect()
    {
        NoteEvent[] target =
        [
            new("C4", 0), new("D4", 100), new("E4", 200), new("F4", 300), new("G4", 400),
            new("A4", 500), new("B4", 600), new("C5", 700), new("D4", 800),
        ];
        NoteEvent[] attempt =
        [
            new("C4", 0), new("D4", 100), new("E4", 200), new("F4", 300), new("G4", 400),
            new("A4", 500), new("C5", 600), new("D4", 700), new("E4", 800), // diverges at index 6
        ];

        Assert.Equal(6.0 / 9.0, NoteComparer.PrefixRatio(target, attempt), precision: 3);
    }
}
