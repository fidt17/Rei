using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Trait("Category", "Unit")]
[Trait("Area", "Monitor")]
public sealed class EntityInfoComponentDrawerViewModelTests
{
    private sealed class Rename(Func<EntityRenameCommandTarget, Task<EntityRenameCommandResult>> execute) : IEntityRenameCommand
    {
        public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target) => execute(target);
    }

    [Fact]
    public async Task FailedAsyncRenameIsLoggedAndRestoresModelName()
    {
        var entity = new GameEntity(17, "Before");
        var completion = new TaskCompletionSource<EntityRenameCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new Rename(_ => completion.Task);
        var logger = new TestLogger<EntityInfoComponentDrawerViewModel>();
        using var drawer = new EntityInfoComponentDrawerViewModel(entity, command, logger);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        drawer.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(drawer.EntityName) && drawer.EntityName == "Before") restored.TrySetResult();
        };
        drawer.EntityName = "Requested";
        Assert.Equal("Requested", drawer.EntityName);
        var error = new InvalidOperationException("rename failed");

        completion.SetException(error);
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Before", drawer.EntityName);
        Assert.Equal("Before", entity.Name);
        Assert.Same(error, Assert.Single(logger.Entries).Exception);
    }

    [Fact]
    public void InitializationAndModelUpdatesDoNotExecuteRename()
    {
        var entity = new GameEntity(17, "Before");
        var management = new TestEntityManagementService();
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), new TestSceneManagementService(entity));
        using var drawer = new EntityInfoComponentDrawerViewModel(entity, command, new TestLogger<EntityInfoComponentDrawerViewModel>());
        Assert.Equal("Before", drawer.EntityName);
        Assert.Empty(management.RenameCalls);
        var notifications = new List<string?>();
        drawer.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        entity.SetName("From model");

        Assert.Equal("From model", drawer.EntityName);
        Assert.Equal(new[] { nameof(drawer.EntityName) }, notifications);
        Assert.Empty(management.RenameCalls);
    }

    [Theory]
    [InlineData("After", "After")]
    [InlineData("", "Entity 17")]
    public void UserEditExecutesRenameOnceAndReflectsModelName(string requestedName, string appliedName)
    {
        var entity = new GameEntity(17, "Before");
        var management = new TestEntityManagementService { OnRename = (target, _) => target.SetName(appliedName) };
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), new TestSceneManagementService(entity));
        using var drawer = new EntityInfoComponentDrawerViewModel(entity, command, new TestLogger<EntityInfoComponentDrawerViewModel>());

        drawer.EntityName = requestedName;

        var call = Assert.Single(management.RenameCalls);
        Assert.Same(entity, call.Entity);
        Assert.Equal(requestedName, call.Name);
        Assert.Equal(appliedName, entity.Name);
        Assert.Equal(appliedName, drawer.EntityName);
        drawer.EntityName = appliedName;
        Assert.Single(management.RenameCalls);
    }
}
