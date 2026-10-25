using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Trait("Category", "Unit")]
[Trait("Area", "Monitor")]
public sealed class EntityInfoComponentDrawerViewModelTests
{
    [Fact]
    public void InitializationAndModelUpdatesDoNotExecuteRename()
    {
        var entity = new GameEntity(17, "Before");
        var management = new TestEntityManagementService();
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
        using var drawer = new EntityInfoComponentDrawerViewModel(entity, command);
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
        var command = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>());
        using var drawer = new EntityInfoComponentDrawerViewModel(entity, command);

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
