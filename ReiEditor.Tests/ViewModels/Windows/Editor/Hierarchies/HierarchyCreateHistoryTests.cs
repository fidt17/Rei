using Avalonia.Headless.XUnit;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Hierarchies;

[Collection(HeadlessCollection.NAME)]
public sealed class HierarchyCreateHistoryTests
{
    [AvaloniaFact]
    public async Task RootCreateAddsOneEntryAndRedoRejectsAnOccupiedId()
    {
        var sibling = new GameEntity(1, "Sibling");
        var scenes = new TestSceneManagementService(sibling);
        var scene = scenes.CurrentScene.Value!;
        var management = new TestEntityManagementService
        {
            OnCreate = (name, _) =>
            {
                var entity = new GameEntity(scene.AllocateEntityId(), name);
                entity.AddBehaviour(new BehaviourComponent(2));
                scene.AddEntity(entity);
                return Task.FromResult<GameEntity?>(entity);
            },
            OnDestroy = scene.DeleteEntity,
            OnRecreate = entity =>
            {
                scene.AddEntity(entity);
                return Task.FromResult<GameEntity?>(entity);
            }
        };
        using var commands = new EditorCommandService();
        using var hierarchy = new HierarchyDragFixture(scene.Hierarchy, management, scenes, commands);
        hierarchy.ViewModel.RootContextMenu.Options.Single(option => option.Text == "New Entity").Command.Execute(null);

        var created = hierarchy.ViewModel.Nodes.Last().Node.Content;
        Assert.Equal("New Entity", created.Name);
        await commands.UndoAsync();
        Assert.Same(sibling, Assert.Single(hierarchy.ViewModel.Nodes).Node.Content);
        Assert.False(commands.CanUndo);

        var occupant = new GameEntity(created.Id, "Occupied");
        scene.AddEntity(occupant);
        Assert.False(commands.CanRedo);
        await Assert.ThrowsAsync<InvalidOperationException>(commands.RedoAsync);
        Assert.Same(occupant, scene.GetById(created.Id));

        scene.DeleteEntity(occupant);
        Assert.True(commands.CanRedo);
        await commands.RedoAsync();
        Assert.Same(created, hierarchy.ViewModel.Nodes.Last().Node.Content);
        Assert.False(commands.CanRedo);
    }
}
