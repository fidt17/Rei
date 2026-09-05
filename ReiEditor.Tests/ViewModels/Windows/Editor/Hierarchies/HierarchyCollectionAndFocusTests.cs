using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Hierarchies;

/// <summary>
/// Verifies hierarchy VM collection ownership and focus expansion behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Hierarchy")]
public sealed class HierarchyCollectionAndFocusTests
{
    private sealed class TestEntityRenameCommand : IEntityRenameCommand
    {
        public void Execute(EntityRenameCommandTarget target) { }
    }

    private sealed class TestSelectedEntityActionService : ISelectedEntityActionService
    {
        public event Action<int>? RenameEntityRequested;
        public bool DeleteSelectedEntity() => true;
        public bool DuplicateSelectedEntity() => true;
        public bool RequestRenameSelectedEntity()
        {
            RenameEntityRequested?.Invoke(0);
            return true;
        }
    }

    private sealed class TestNodeFactory(SelectionService selectionService) : IFactory<HierarchyNodeViewModel>
    {
        public HierarchyNodeViewModel CreateInstance() => throw new NotSupportedException();

        public HierarchyNodeViewModel CreateInstance(params object[] parameters)
        {
            return new HierarchyNodeViewModel(
                Assert.IsType<HierarchyNode<GameEntity>>(Assert.Single(parameters)),
                new TestEntityRenameCommand(),
                new TestSelectedEntityActionService(),
                selectionService);
        }
    }

    /// <summary>
    /// Initial hierarchy preserves root and child order, restores expansion, and registers every node once.
    /// </summary>
    [Fact]
    public void TestSetHierarchyBuildsOrderedTreeAndRestoresExpansion()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = TestCreateHierarchy(out var firstRoot, out var child, out var secondRoot);
        var configured = new List<HierarchyNodeViewModel>();
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), configured.Add);

        controller.SetHierarchy(hierarchy, new HashSet<int> { firstRoot.Content.Id });

        Assert.Equal(new[] { 1, 3 }, controller.Nodes.Select(node => node.Node.Content.Id));
        var rootVm = controller.FindByEntityId(firstRoot.Content.Id)!;
        Assert.Equal(child.Content.Id, Assert.Single(rootVm.ChildNodes).Node.Content.Id);
        Assert.True(rootVm.Expanded.Value);
        Assert.Equal(3, configured.Count);
        Assert.All(new[] { firstRoot, child, secondRoot }, node => Assert.NotNull(selection.GetEntitySelectable(node.Content)));

        controller.Dispose();
    }

    /// <summary>
    /// Moving roots updates observable order without recreating VMs.
    /// </summary>
    [Fact]
    public void TestMoveRootReordersExistingViewModels()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = TestCreateHierarchy(out var firstRoot, out _, out var secondRoot);
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), _ => { });
        controller.SetHierarchy(hierarchy, new HashSet<int>());
        var secondVm = controller.FindByEntityId(secondRoot.Content.Id);

        Assert.True(hierarchy.MoveNode(secondRoot, null, 0));

        Assert.Equal(new[] { 3, 1 }, controller.Nodes.Select(node => node.Node.Content.Id));
        Assert.Same(secondVm, controller.Nodes[0]);
        controller.Dispose();
    }

    /// <summary>Extreme and forward insertion indices keep model and VM sibling order aligned without recreating VMs.</summary>
    [Theory]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public void TestClampedMoveKeepsViewModelOrderAndIdentity(bool nested, int insertionIndex)
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = new Hierarchy<GameEntity>("Scene");
        var parent = nested ? new HierarchyNode<GameEntity>(new GameEntity(10, "Parent"), null) : null;
        if (parent != null) hierarchy.AddNode(parent, isRoot: true);
        var nodes = Enumerable.Range(1, 3).Select(id => new HierarchyNode<GameEntity>(new GameEntity(id, $"Entity {id}"), parent)).ToArray();
        foreach (var node in nodes)
        {
            parent?.PushChild(node);
            hierarchy.AddNode(node, isRoot: parent == null);
        }
        var created = new List<HierarchyNodeViewModel>();
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), created.Add);
        try
        {
            controller.SetHierarchy(hierarchy, new HashSet<int>());
            var originalVms = nodes.Select(node => controller.FindByEntityId(node.Content.Id)).ToArray();

            Assert.True(hierarchy.MoveNode(nodes[0], parent, insertionIndex));

            var viewModels = parent == null ? controller.Nodes : controller.FindByEntityId(parent.Content.Id)!.ChildNodes;
            Assert.Equal(new[] { 2, 3, 1 }, viewModels.Select(vm => vm.Node.Content.Id));
            Assert.Equal((parent?.ChildNodes ?? hierarchy.RootNodes).Select(node => node.Content.Id), viewModels.Select(vm => vm.Node.Content.Id));
            Assert.Equal(nested ? 4 : 3, created.Count);
            Assert.Equal(new[] { originalVms[1], originalVms[2], originalVms[0] }, viewModels);
        }
        finally
        {
            controller.Dispose();
            // Descendant disposal is a separate known regression; explicitly release every created VM here.
            foreach (var viewModel in created) viewModel.Dispose();
        }
    }

    /// <summary>
    /// Scene-style add as root followed by move under parent relocates same VM without duplicate collection entries.
    /// </summary>
    [Fact]
    public void TestDynamicAddThenParentMoveRelocatesSingleViewModel()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = new Hierarchy<GameEntity>("Scene");
        var parent = new HierarchyNode<GameEntity>(new GameEntity(1, "Parent"), null);
        hierarchy.AddNode(parent, isRoot: true);
        var configured = new List<HierarchyNodeViewModel>();
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), configured.Add);
        controller.SetHierarchy(hierarchy, new HashSet<int>());
        var child = new HierarchyNode<GameEntity>(new GameEntity(2, "Child"), null);

        hierarchy.AddNode(child, isRoot: true);
        var addedVm = controller.FindByEntityId(child.Content.Id);
        Assert.True(hierarchy.MoveNode(child, parent, 0));

        var parentVm = Assert.Single(controller.Nodes);
        Assert.Same(addedVm, Assert.Single(parentVm.ChildNodes));
        Assert.Equal(2, configured.Count);
        Assert.Equal(2, controller.GetAllNodes().Count());
        controller.Dispose();
    }

    /// <summary>
    /// Removing subtree disposes its VMs and removes their selectable registrations and lookup entries.
    /// </summary>
    [Fact]
    public void TestRemoveSubtreeDisposesNodesAndClearsLookups()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = TestCreateHierarchy(out var firstRoot, out var child, out _);
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), _ => { });
        controller.SetHierarchy(hierarchy, new HashSet<int>());

        hierarchy.DeleteNode(firstRoot);

        Assert.Null(controller.FindByEntityId(firstRoot.Content.Id));
        Assert.Null(controller.FindByEntityId(child.Content.Id));
        Assert.Null(selection.GetEntitySelectable(firstRoot.Content));
        Assert.Null(selection.GetEntitySelectable(child.Content));
        controller.Dispose();
    }

    /// <summary>
    /// Disposing collection controller unregisters root and descendant selectable VMs.
    /// </summary>
    [Fact]
    public void TestDisposeUnregistersEveryNodeInTree()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = TestCreateHierarchy(out var firstRoot, out var child, out var secondRoot);
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), _ => { });
        controller.SetHierarchy(hierarchy, new HashSet<int>());

        controller.Dispose();

        Assert.Null(selection.GetEntitySelectable(firstRoot.Content));
        Assert.Null(selection.GetEntitySelectable(child.Content));
        Assert.Null(selection.GetEntitySelectable(secondRoot.Content));
    }

    /// <summary>
    /// Focusing nested entity expands every ancestor and emits target scroll ID.
    /// </summary>
    [Fact]
    public void TestActiveSelectionFocusExpandsAncestorsAndScrollsToTarget()
    {
        var selection = new SelectionService(new TestEntityApi());
        var hierarchy = TestCreateHierarchy(out var firstRoot, out var child, out _);
        var controller = new HierarchyNodeCollectionController(new TestNodeFactory(selection), _ => { });
        controller.SetHierarchy(hierarchy, new HashSet<int>());
        var childVm = controller.FindByEntityId(child.Content.Id)!;
        var scrolledIds = new List<int>();
        var focus = new HierarchyFocusController(controller.FindByEntityId, _ => { }, scrolledIds.Add);

        focus.HandleActiveSelectionChanged(childVm);

        Assert.True(controller.FindByEntityId(firstRoot.Content.Id)!.Expanded.Value);
        Assert.Equal(new[] { child.Content.Id }, scrolledIds);
        controller.Dispose();
    }

    /// <summary>
    /// Missing and non-entity focus targets leave expansion and scroll state unchanged.
    /// </summary>
    [Fact]
    public void TestMissingFocusTargetDoesNothing()
    {
        var scrolledIds = new List<int>();
        var focus = new HierarchyFocusController(_ => null, _ => throw new InvalidOperationException(), scrolledIds.Add);

        focus.HandleActiveSelectionChanged(null);

        Assert.Empty(scrolledIds);
    }

    /// <summary>
    /// Creates two roots with one child already attached to first root.
    /// </summary>
    private static Hierarchy<GameEntity> TestCreateHierarchy(
        out HierarchyNode<GameEntity> firstRoot,
        out HierarchyNode<GameEntity> child,
        out HierarchyNode<GameEntity> secondRoot)
    {
        var hierarchy = new Hierarchy<GameEntity>("Scene");
        firstRoot = new HierarchyNode<GameEntity>(new GameEntity(1, "First"), null);
        child = new HierarchyNode<GameEntity>(new GameEntity(2, "Child"), firstRoot);
        secondRoot = new HierarchyNode<GameEntity>(new GameEntity(3, "Second"), null);
        firstRoot.PushChild(child);
        hierarchy.AddNode(firstRoot, isRoot: true);
        hierarchy.AddNode(child, isRoot: false);
        hierarchy.AddNode(secondRoot, isRoot: true);
        return hierarchy;
    }
}
