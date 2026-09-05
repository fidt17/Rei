using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Hierarchies;

/// <summary>
/// Verifies hierarchy node rename, action routing, name synchronization, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Hierarchy")]
public sealed class HierarchyNodeViewModelTests
{
    private sealed class TestEntityRenameCommand : IEntityRenameCommand
    {
        public List<EntityRenameCommandTarget> Targets { get; } = new();
        public void Execute(EntityRenameCommandTarget target) => Targets.Add(target);
    }

    private sealed class TestSelectedEntityActionService : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested;
        public int DeleteCount { get; private set; }
        public int DuplicateCount { get; private set; }
        public bool DeleteSelectedEntity()
        {
            DeleteCount++;
            return true;
        }

        public bool DuplicateSelectedEntity()
        {
            DuplicateCount++;
            return true;
        }

        public bool RequestRenameSelectedEntity()
        {
            RenameEntityRequested?.Invoke(0);
            return true;
        }
    }

    /// <summary>
    /// Starting rename copies current display name and confirming forwards exact entered value with entity identity.
    /// </summary>
    [Fact]
    public void TestRenameCommandsInitializeAndForwardValue()
    {
        var entity = new GameEntity(7, "Original");
        var rename = new TestEntityRenameCommand();
        var selection = new SelectionService(new TestEntityApi());
        var vm = new HierarchyNodeViewModel(
            new HierarchyNode<GameEntity>(entity, null), rename, new TestSelectedEntityActionService(), selection);

        vm.StartRenameCommand.Execute(null);
        vm.ConfirmRenameCommand.Execute("  Renamed  ");

        Assert.Equal("Original", vm.RenameValue.Value);
        var target = Assert.Single(rename.Targets);
        Assert.Same(entity, target.Entity);
        Assert.Equal("  Renamed  ", target.Name);
        vm.Dispose();
    }

    /// <summary>
    /// Duplicate and delete commands route once to selected-entity action service.
    /// </summary>
    [Fact]
    public void TestEntityActionCommandsRouteToService()
    {
        var actions = new TestSelectedEntityActionService();
        var selection = new SelectionService(new TestEntityApi());
        var vm = new HierarchyNodeViewModel(
            new HierarchyNode<GameEntity>(new GameEntity(1, "Entity"), null), new TestEntityRenameCommand(), actions, selection);

        vm.DuplicateCommand.Execute(null);
        vm.DeleteCommand.Execute(null);

        Assert.Equal(1, actions.DuplicateCount);
        Assert.Equal(1, actions.DeleteCount);
        vm.Dispose();
    }

    /// <summary>
    /// Entity name changes update live VM, while disposal unsubscribes and unregisters selectable.
    /// </summary>
    [Fact]
    public void TestDisposeStopsNameSynchronizationAndUnregistersSelectable()
    {
        var entity = new GameEntity(4, "Before");
        var selection = new SelectionService(new TestEntityApi());
        var vm = new HierarchyNodeViewModel(
            new HierarchyNode<GameEntity>(entity, null), new TestEntityRenameCommand(), new TestSelectedEntityActionService(), selection);
        entity.SetName("Live");
        Assert.Equal("Live", vm.Name.Value);

        vm.Dispose();
        entity.SetName("After dispose");

        Assert.Equal("Live", vm.Name.Value);
        Assert.Null(selection.GetEntitySelectable(entity));
    }
}
