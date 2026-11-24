using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Mcp.Editor;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Mcp;

[Trait("Area", "Mcp")]
public sealed class McpEntityRenameTests
{
    private sealed class Rename(Func<EntityRenameCommandTarget, Task<EntityRenameCommandResult>> execute) : IEntityRenameCommand
    {
        public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target) => execute(target);
    }

    [Theory]
    [InlineData("After", true)]
    [InlineData("Before", false)]
    public async Task RenameReturnsCommandChangeResult(string name, bool changed)
    {
        var scenes = CreateScene();
        var management = new TestEntityManagementService { OnRename = (target, value) => target.SetName(value) };
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, new TestEngineRunner());
        using var session = Create(scenes, rename);

        var result = await session.RenameEntityAsync(1, name);

        Assert.Equal(changed, result.Changed);
        Assert.Equal(name, scenes.CurrentScene.Value!.GetById(1)!.Name);
        Assert.Single(management.RenameCalls);
    }

    [Fact]
    public async Task UnappliedRenameRemainsExplicitFailure()
    {
        var scenes = CreateScene();
        var management = new TestEntityManagementService { OnRename = (_, _) => { } };
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, new TestEngineRunner());
        using var session = Create(scenes, rename);

        var error = await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.RenameEntityAsync(1, "After"));

        Assert.Equal("rename_failed", error.Code);
        Assert.Equal("Before", scenes.CurrentScene.Value!.GetById(1)!.Name);
    }

    [Fact]
    public async Task RenameAwaitsPendingCommandResult()
    {
        var scenes = CreateScene();
        var completion = new TaskCompletionSource<EntityRenameCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rename = new Rename(target =>
        {
            target.Entity.SetName(target.Name);
            return completion.Task;
        });
        using var session = Create(scenes, rename);

        var operation = session.RenameEntityAsync(1, "After");
        Assert.False(operation.IsCompleted);
        completion.SetResult(new("Before", "After"));

        Assert.True((await operation).Changed);
    }

    [Fact]
    public async Task RenameUndoRedoUseSharedServiceAndRejectUnavailableReplay()
    {
        var scenes = CreateScene();
        var management = new TestEntityManagementService { OnRename = (target, value) => target.SetName(value) };
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, new TestEngineRunner());
        using var session = Create(scenes, rename);
        Assert.Equal("undo_unavailable", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.UndoAsync())).Code);
        await session.RenameEntityAsync(1, "After");
        Assert.True(session.GetCommandState().CanUndo);
        Assert.True((await session.UndoAsync()).CanRedo);
        Assert.Equal("Before", scenes.CurrentScene.Value!.GetById(1)!.Name);
        Assert.True((await session.RedoAsync()).CanUndo);
        Assert.Equal("After", scenes.CurrentScene.Value!.GetById(1)!.Name);
        Assert.Equal("redo_unavailable", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.RedoAsync())).Code);

        management.OnRename = (_, _) => throw new InvalidOperationException("rename failed");
        Assert.Equal("undo_failed", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.UndoAsync())).Code);
        Assert.True(session.GetCommandState().CanUndo);
    }

    [Fact]
    public async Task PendingCommandIsReportedBusyAndCannotBeUndone()
    {
        var scenes = CreateScene();
        var completion = new TaskCompletionSource<EntityRenameCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = Create(scenes, new Rename(_ => completion.Task));
        var pending = session.RenameEntityAsync(1, "Before");
        Assert.True(session.GetCommandState().IsBusy);
        Assert.Equal("command_in_progress", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.RenameEntityAsync(1, "Concurrent"))).Code);
        Assert.Equal("command_in_progress", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.UndoAsync())).Code);
        completion.SetResult(new("Before", "Before"));
        await pending;
        Assert.False(session.GetCommandState().IsBusy);
    }

    [Fact]
    public async Task PlayRenameReturnsExplicitFailureWithoutMutation()
    {
        var scenes = CreateScene();
        var runner = new TestEngineRunner();
        runner.PlaymodeActive.Value = true;
        var management = new TestEntityManagementService { OnRename = (target, value) => target.SetName(value) };
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, runner);
        using var session = Create(scenes, rename);
        Assert.Equal("rename_failed", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => session.RenameEntityAsync(1, "Blocked"))).Code);
        Assert.Empty(management.RenameCalls);
        Assert.Equal("Before", scenes.Scene.Value!.GetById(1)!.Name);
    }

    private static TestSceneManagementService CreateScene()
    {
        var scenes = new TestSceneManagementService();
        var scene = new Scene("Rename");
        scene.AddEntity(new GameEntity(1, "Before"));
        scenes.Scene.Value = scene;
        return scenes;
    }

    private static McpEditorSessionRegistration Create(ISceneManagementService scenes, IEntityRenameCommand rename)
    {
        var commands = new EditorCommandService();
        var decorated = new EntityRenameCommandDecorator(rename, commands);
        return new(new McpEditorSessionAccessor(), null!, scenes, null!, decorated, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, commands, null!);
    }
}
