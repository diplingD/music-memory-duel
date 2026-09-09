using Server.Core.Models;
using Server.Validators;

namespace Server.Tests;

public class SubmissionValidatorTests
{
    [Fact]
    public void EmptySequence_IsInvalid()
    {
        Assert.False(SubmissionValidator.IsValid([]));
    }

    [Fact]
    public void SingleNote_IsValid()
    {
        Assert.True(SubmissionValidator.IsValid([new NoteEvent("C4", 0)]));
    }

    [Fact]
    public void TooManyNotes_IsInvalid()
    {
        var notes = Enumerable.Range(0, 101).Select(i => new NoteEvent("C4", i * 10)).ToArray();
        Assert.False(SubmissionValidator.IsValid(notes));
    }

    [Fact]
    public void MaxNotes_IsValid()
    {
        var notes = Enumerable.Range(0, 100).Select(i => new NoteEvent("C4", i * 10)).ToArray();
        Assert.True(SubmissionValidator.IsValid(notes));
    }

    [Fact]
    public void IncreasingTMs_IsValid()
    {
        NoteEvent[] notes = [new("C4", 0), new("D4", 400), new("E4", 900)];
        Assert.True(SubmissionValidator.IsValid(notes));
    }

    [Fact]
    public void EqualConsecutiveTMs_IsValid()
    {
        NoteEvent[] notes = [new("C4", 0), new("D4", 400), new("E4", 400)];
        Assert.True(SubmissionValidator.IsValid(notes));
    }

    [Fact]
    public void DecreasingTMs_IsInvalid()
    {
        NoteEvent[] notes = [new("C4", 0), new("D4", 400), new("E4", 300)];
        Assert.False(SubmissionValidator.IsValid(notes));
    }
}
