using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies procedure identity and completion state visible to subscribers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class ProcedureTests
{
    /// <summary>
    /// A procedure preserves its name and becomes finished before notifying completion subscribers.
    /// </summary>
    [Fact]
    public void CompletePublishesFinishedState()
    {
        var procedure = new Procedure("Import assets");
        var observedStates = new List<bool>();
        procedure.FinishedEvent += () => observedStates.Add(procedure.Finished);
        Assert.Equal("Import assets", procedure.Name);
        Assert.False(procedure.Finished);

        procedure.Complete();

        Assert.True(procedure.Finished);
        Assert.Equal(new[] { true }, observedStates);
    }

    /// <summary>Unknown work stays indeterminate; reports are bounded, monotonic and stop after completion.</summary>
    [Fact]
    public void ProgressIsMonotonicAndCompletionDoesNotInventSuccess()
    {
        var procedure = new Procedure("Load");
        var observed = new List<double?>();
        var completions = 0;
        procedure.ProgressChangedEvent += () => observed.Add(procedure.Progress);
        procedure.FinishedEvent += () => completions++;
        Assert.Null(procedure.Progress);
        procedure.ReportProgress(-1);
        procedure.ReportProgress(0.4);
        procedure.ReportProgress(0.2);
        procedure.ReportProgress(0.4);
        procedure.Complete();
        procedure.Complete();
        procedure.ReportProgress(1);
        Assert.Equal(new double?[] { 0, 0.4 }, observed);
        Assert.Equal(0.4, procedure.Progress);
        Assert.Equal(1, completions);
        var successful = new Procedure("Done");
        successful.ReportProgress(2);
        Assert.Equal(1, successful.Progress);
        Assert.Throws<ArgumentOutOfRangeException>(() => successful.ReportProgress(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => successful.ReportProgress(double.PositiveInfinity));
    }

    /// <summary>Parallel producers retain the largest completed-work fraction.</summary>
    [Fact]
    public void ParallelProgressReportsRetainGreatestValue()
    {
        var procedure = new Procedure("Parallel");
        Parallel.For(0, 1001, index => procedure.ReportProgress(index / 1000d));
        Assert.Equal(1, procedure.Progress);
    }
}
