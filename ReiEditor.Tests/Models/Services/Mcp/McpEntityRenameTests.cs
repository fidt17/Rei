using ReiEditor.Mcp.Contracts;
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
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
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
        var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
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

    private static TestSceneManagementService CreateScene()
    {
        var scenes = new TestSceneManagementService();
        var scene = new Scene("Rename");
        scene.AddEntity(new GameEntity(1, "Before"));
        scenes.Scene.Value = scene;
        return scenes;
    }

    private static McpEditorSessionRegistration Create(ISceneManagementService scenes, IEntityRenameCommand rename) =>
        new(new McpEditorSessionAccessor(), null!, scenes, null!, rename, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
}
