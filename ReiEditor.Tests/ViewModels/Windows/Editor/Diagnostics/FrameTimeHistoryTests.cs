using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Diagnostics;

public sealed class FrameTimeHistoryTests
{
    private static ProfilingSnapshot Sample(ulong frame, double milliseconds = 20) => new()
    {
        LastFrame = frame, CompletedFrames = 120, SampleFrames = 120, AverageFrameMs = milliseconds
    };

    [Fact]
    public void HistoryKeepsTwentySecondsAndDoesNotInventPointsForRetainedSnapshots()
    {
        var history = new FrameTimeHistory();
        Assert.True(history.Append(Sample(120), 0));
        Assert.False(history.Append(Sample(120), 0.5));
        Assert.True(history.Append(Sample(240), 20));
        Assert.Equal(2, history.Samples.Count);
        Assert.True(history.Append(Sample(241), 20.5));
        Assert.Equal(new[] { 20.0, 20.5 }, history.Samples.Select(sample => sample.Seconds));
        Assert.False(history.Append(Sample(241), 60));
        Assert.Equal(2, history.Samples.Count);
        Assert.True(history.Append(Sample(242), 60));
        Assert.Single(history.Samples);
        Assert.Equal(60, history.Samples[0].Seconds);
    }

    [Fact]
    public void InvalidMeasurementsAreExcludedAndClearAllowsSameFrameInNewSession()
    {
        var history = new FrameTimeHistory();
        Assert.False(history.Append(Sample(1, double.NaN), 0));
        Assert.False(history.Append(Sample(1, 0), 0));
        Assert.False(history.Append(Sample(1) with { SampleFrames = 0 }, 0));
        Assert.Empty(history.Samples);
        Assert.True(history.Append(Sample(1), 0));
        history.Clear();
        Assert.Empty(history.Samples);
        Assert.True(history.Append(Sample(1, 30), 1));
        Assert.Equal(30, history.Samples[0].Milliseconds);
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(0.0001, "<0.01")]
    [InlineData(0.009, "<0.01")]
    [InlineData(0.01, "0.01")]
    [InlineData(6.099, "6.10")]
    public void MillisecondDisplayUsesTwoDecimalsWithoutHidingTinyMeasurements(double value, string expected)
    {
        Assert.Equal(expected, ProfilingPresentation.Time(value));
    }
}
