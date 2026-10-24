using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Mcp.Editor;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Tests.Models.Services.Mcp;

/// <summary>Verifies existing MCP operation progress follows matching application procedures.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Mcp")]
public sealed class McpProcedureProgressTests
{
    /// <summary>Matching work advances progress; unrelated work and late reports cannot complete the operation.</summary>
    [Theory]
    [InlineData(ReiOperationKinds.OPEN_SCENE, ProcedureTags.LOADING_SCENE, false)]
    [InlineData(ReiOperationKinds.BUILD_PROJECT, ProcedureTags.BUILD_PROJECT, true)]
    [InlineData(ReiOperationKinds.REFRESH_ASSETS, ProcedureTags.IMPORT_ASSETS, false)]
    public async Task MatchingProcedureReportsProgressUntilOperationResult(string kind, string name, bool fails)
    {
        var procedures = new EditorProceduresService();
        using var coordinator = new McpEditorOperationCoordinator(new EditorConsoleService(), new TestLogger<McpEditorOperationCoordinator>(), procedures);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = coordinator.Start(kind, async _ =>
        {
            entered.SetResult();
            await release.Task;
            if (fails) throw new InvalidOperationException("failed result");
            return "Done";
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var unrelated = new Procedure("Other");
            procedures.TrackProcedure(unrelated);
            unrelated.ReportProgress(0.9);
            Assert.Equal(0, coordinator.Get(operation.Id).Progress);
            var procedure = new Procedure(name);
            procedures.TrackProcedure(procedure);
            procedure.ReportProgress(0.37);
            Assert.Equal(0.37, coordinator.Get(operation.Id).Progress);
            procedure.ReportProgress(1);
            Assert.True(coordinator.Get(operation.Id).Progress < 1);
            procedure.Complete();
            unrelated.Complete();
        }
        finally { release.TrySetResult(); }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (coordinator.Get(operation.Id).Status is ReiOperationStatuses.QUEUED or ReiOperationStatuses.RUNNING)
            await Task.Delay(10, timeout.Token);
        Assert.Equal(fails ? ReiOperationStatuses.FAILED : ReiOperationStatuses.SUCCEEDED, coordinator.Get(operation.Id).Status);
        Assert.Equal(!fails, coordinator.Get(operation.Id).Progress == 1);
    }
}
