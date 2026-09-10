using Server.Core.Game;
using Server.Core.Models;

namespace Server.Tests;

public class HumanTimingTests
{
    // SPEC 9.2

    [Fact]
    public void PerfectCopyOfTargetTiming_IsNotHumanTiming()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 300), new("E4", 700)];

        Assert.False(HumanTiming.IsHumanTiming(target, attempt));
    }

    [Fact]
    public void IntervalBelowHumanFloor_IsNotHumanTiming()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 30), new("E4", 700)]; // 30ms < MinNoteIntervalMs (50ms)

        Assert.False(HumanTiming.IsHumanTiming(target, attempt));
    }

    [Fact]
    public void HumanJitter_IsHumanTiming()
    {
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 340), new("E4", 660)]; // off by 40/40ms, well above the MAD floor

        Assert.True(HumanTiming.IsHumanTiming(target, attempt));
    }

    [Fact]
    public void ShortAttempt_SkipsMachineCopyCheck()
    {
        // fewer than 3 notes: the exact-copy check (SPEC 8.1) doesn't apply, only the interval floor does
        NoteEvent[] target = [new("C4", 0), new("D4", 300)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 300)];

        Assert.True(HumanTiming.IsHumanTiming(target, attempt));
    }

    [Fact]
    public void DifferentLengthThanTarget_SkipsMachineCopyCheck()
    {
        // length mismatch is NoteComparer's job, not HumanTiming's — this only guards the interval floor
        NoteEvent[] target = [new("C4", 0), new("D4", 300), new("E4", 700)];
        NoteEvent[] attempt = [new("C4", 0), new("D4", 300)];

        Assert.True(HumanTiming.IsHumanTiming(target, attempt));
    }
}
