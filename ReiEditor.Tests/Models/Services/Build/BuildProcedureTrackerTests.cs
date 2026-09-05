using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Build;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies build-state transitions create and complete procedures and disposal removes subscriptions.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class BuildProcedureTrackerTests
{
    /// <summary>Each build transition gets one procedure that completes when build state resets.</summary>
    [Fact]
    public void TracksRepeatedBuildsWithoutLeavingActiveProcedures()
    {
        var build = new TestBuildService();
        var procedures = new EditorProceduresService();
        using var tracker = new BuildProcedureTracker(build, procedures);
        Assert.Empty(procedures.ActiveProcedures);

        for (var run = 0; run < 2; run++)
        {
            build.InProgress.Value = true;
            var procedure = Assert.Single(procedures.ActiveProcedures);
            Assert.Equal(ProcedureTags.BUILD_PROJECT, procedure.Name);
            Assert.False(procedure.Finished);
            build.InProgress.Value = false;
            Assert.True(procedure.Finished);
            Assert.Empty(procedures.ActiveProcedures);
        }
    }

    /// <summary>Disposing an idle tracker prevents future builds from creating procedures.</summary>
    [Fact]
    public void DisposeUnsubscribesFutureBuilds()
    {
        var build = new TestBuildService();
        var procedures = new EditorProceduresService();
        var tracker = new BuildProcedureTracker(build, procedures);
        tracker.Dispose();

        build.InProgress.Value = true;
        build.InProgress.Value = false;

        Assert.Empty(procedures.ActiveProcedures);
    }
}
