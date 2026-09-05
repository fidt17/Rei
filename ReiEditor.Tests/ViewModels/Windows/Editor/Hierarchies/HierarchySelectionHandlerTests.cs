using Avalonia.Input;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Hierarchies;

/// <summary>
/// Verifies hierarchy click, range, anchor, context-menu, and restore selection behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Hierarchy")]
public sealed class HierarchySelectionHandlerTests : IDisposable
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

    private readonly TestEntityApi _entityApi = new();
    private readonly SelectionService _selectionService;
    private readonly List<HierarchyNodeViewModel> _nodes = new();

    public HierarchySelectionHandlerTests()
    {
        _selectionService = new SelectionService(_entityApi);
        for (var i = 1; i <= 5; i++)
        {
            var entity = new GameEntity(i, $"Entity {i}");
            var node = new HierarchyNode<GameEntity>(entity, null);
            _nodes.Add(new HierarchyNodeViewModel(node, new TestEntityRenameCommand(), new TestSelectedEntityActionService(), _selectionService));
        }
    }

    /// <summary>
    /// Releases selectable registrations created for each test.
    /// </summary>
    public void Dispose()
    {
        foreach (var node in _nodes)
        {
            node.Dispose();
        }
    }

    /// <summary>
    /// Plain click replaces selection, Ctrl toggles membership, and removing primary promotes first visible selected node.
    /// </summary>
    [Fact]
    public void TestPlainAndControlClicksUpdateMembershipAndPrimary()
    {
        var handler = TestCreateHandler();

        handler.HandleNodeSelectionRequested(_nodes[1], KeyModifiers.None);
        handler.HandleNodeSelectionRequested(_nodes[3], KeyModifiers.Control);

        Assert.Equal(new[] { 2, 4 }, TestSelectedIds());
        Assert.Same(_nodes[3], _selectionService.ActiveSelection.Value);

        handler.HandleNodeSelectionRequested(_nodes[3], KeyModifiers.Control);

        Assert.Equal(new[] { 2 }, TestSelectedIds());
        Assert.Same(_nodes[1], _selectionService.ActiveSelection.Value);
    }

    /// <summary>
    /// Shift selects an inclusive reverse range from stable anchor and Ctrl+Shift preserves earlier members.
    /// </summary>
    [Fact]
    public void TestShiftAndControlShiftUseInclusiveStableAnchor()
    {
        var handler = TestCreateHandler();
        handler.HandleNodeSelectionRequested(_nodes[3], KeyModifiers.None);

        handler.HandleNodeSelectionRequested(_nodes[1], KeyModifiers.Shift);
        Assert.Equal(new[] { 2, 3, 4 }, TestSelectedIds());

        handler.HandleNodeSelectionRequested(_nodes[0], KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Equal(new[] { 1, 2, 3, 4 }, TestSelectedIds());
        Assert.Same(_nodes[0], _selectionService.ActiveSelection.Value);
    }

    /// <summary>
    /// Missing anchor falls back to target and a target outside visible traversal replaces selection.
    /// </summary>
    [Fact]
    public void TestMissingAnchorAndInvisibleTargetFallBackToTarget()
    {
        var visible = _nodes.Take(4).ToList();
        var handler = TestCreateHandler(() => visible);
        handler.HandleNodeSelectionRequested(_nodes[1], KeyModifiers.None);
        visible.Remove(_nodes[1]);

        handler.HandleNodeSelectionRequested(_nodes[3], KeyModifiers.Shift);
        Assert.Equal(new[] { 4 }, TestSelectedIds());

        handler.HandleNodeSelectionRequested(_nodes[4], KeyModifiers.Shift);
        Assert.Equal(new[] { 5 }, TestSelectedIds());
    }

    /// <summary>
    /// Context click on selected node preserves membership while making that node primary.
    /// </summary>
    [Fact]
    public void TestContextMenuOnSelectedNodePreservesSelectionAndChangesPrimary()
    {
        var handler = TestCreateHandler();
        handler.HandleNodeSelectionRequested(_nodes[0], KeyModifiers.None);
        handler.HandleNodeSelectionRequested(_nodes[2], KeyModifiers.Control);

        handler.HandleNodeContextMenuSelectionRequested(_nodes[0]);

        Assert.Equal(new[] { 1, 3 }, TestSelectedIds());
        Assert.Same(_nodes[0], _selectionService.ActiveSelection.Value);
    }

    /// <summary>
    /// Restoring remembered hierarchy state avoids a native engine selection request.
    /// </summary>
    [Fact]
    public void TestRestoreSelectionDoesNotSendSelectionToEngine()
    {
        var handler = TestCreateHandler();
        handler.HandleSelectionChanged(new ISelectable[] { _nodes[1], _nodes[2] });

        handler.RestoreSelection();

        Assert.Empty(_entityApi.Selections);
        Assert.Equal(new[] { 2, 3 }, TestSelectedIds());
    }

    /// <summary>
    /// Creates handler against current test nodes and supplied visible traversal.
    /// </summary>
    private HierarchySelectionHandler TestCreateHandler(Func<IReadOnlyList<HierarchyNodeViewModel>>? visibleNodes = null)
    {
        return new HierarchySelectionHandler(
            _selectionService,
            () => _nodes,
            visibleNodes ?? (() => _nodes));
    }

    /// <summary>
    /// Returns selected entity IDs in display order.
    /// </summary>
    private int[] TestSelectedIds()
    {
        return _nodes
            .Where(node => _selectionService.SelectedItems.Contains(node))
            .Select(node => node.Node.Content.Id)
            .ToArray();
    }
}
