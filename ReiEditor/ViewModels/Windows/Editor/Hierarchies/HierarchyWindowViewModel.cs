using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyWindowViewModel : BaseViewModel
{
    public event Action<int>? ScrollToEntityRequested;

    public ICommand ResetSelectionCommand { get; }

    public ObservableField<string> SceneName { get; } = new("Scene Name");
    public ObservableCollection<HierarchyNodeViewModel> Nodes => _nodeCollectionController.Nodes;

    public ContextMenuViewModel RootContextMenu { get; } = new();

    private readonly ISelectionService _selectionService;
    private readonly CreateSceneEntityCommand _createSceneEntityCommand;
    private readonly ISelectedEntityActionService _selectedEntityActionService;
    private readonly IEntityManagementService _entityManagementService;
    private readonly HierarchyNodeCollectionController _nodeCollectionController;
    private readonly HierarchySelectionHandler _selectionHandler;
    private readonly HierarchyFocusController _focusController;

    private Hierarchy<GameEntity>? _activeHierarchy;

#pragma warning disable CS8618
    public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

    public HierarchyWindowViewModel(
        Hierarchy<GameEntity> hierarchy,
        ISelectionService selectionService,
        IFactory<HierarchyNodeViewModel> hierarchyElementFactory,
        IFactory<CreateSceneEntityCommand> createSceneEntityCommand,
        ISelectedEntityActionService selectedEntityActionService,
        IEntityManagementService entityManagementService)
    {
        _activeHierarchy = hierarchy;
        _selectionService = selectionService;
        _createSceneEntityCommand = createSceneEntityCommand.CreateInstance();
        _selectedEntityActionService = selectedEntityActionService;
        _entityManagementService = entityManagementService;

        _nodeCollectionController = new HierarchyNodeCollectionController(hierarchyElementFactory, ConfigureNode);
        _selectionHandler = new HierarchySelectionHandler(
            selectionService,
            () => _nodeCollectionController.GetAllNodes().ToList(),
            GetVisibleNodes);
        _focusController = new HierarchyFocusController(
            entityId => _nodeCollectionController.FindByEntityId(entityId),
            _selectionHandler.ReplaceSelection,
            entityId => ScrollToEntityRequested?.Invoke(entityId));

        SetHierarchy(hierarchy);

        ResetSelectionCommand = ReactiveCommand.Create(_selectionHandler.ResetSelection);
        RootContextMenu.AddOption(new ContextMenuOption("New Entity", ExecuteCreateNewEntityContextMenu));
        _selectedEntityActionService.RenameEntityRequested += _focusController.HandleRenameEntityRequested;
        _selectionService.SelectionChanged.Subscribe(_selectionHandler.HandleSelectionChanged);
        _selectionService.ActiveSelection.Subscribe(_focusController.HandleActiveSelectionChanged);
    }

    public override void Dispose()
    {
        _createSceneEntityCommand.Dispose();
        _selectedEntityActionService.RenameEntityRequested -= _focusController.HandleRenameEntityRequested;
        _selectionService.SelectionChanged.Unsubscribe(_selectionHandler.HandleSelectionChanged);
        _selectionService.ActiveSelection.Unsubscribe(_focusController.HandleActiveSelectionChanged);
        _nodeCollectionController.Dispose();
    }

    public void SetHierarchy(Hierarchy<GameEntity> hierarchy)
    {
        _activeHierarchy = hierarchy;

        var expandedEntityIds = _nodeCollectionController.CaptureExpandedEntityIds();
        SceneName.Set(hierarchy.Name);
        _nodeCollectionController.SetHierarchy(hierarchy, expandedEntityIds);
        _selectionHandler.RestoreSelection();
        _focusController.HandleActiveSelectionChanged(_selectionService.ActiveSelection.Value);
    }

    private void ConfigureNode(HierarchyNodeViewModel node)
    {
        node.ConfigureSelectionActions(
            _selectionHandler.HandleNodeSelectionRequested,
            _selectionHandler.HandleNodeContextMenuSelectionRequested,
            ExecuteCreateChildEntityContextMenu);
    }

    private async void ExecuteCreateNewEntityContextMenu()
    {
        await CreateEntityAndStartRename(null);
    }

    private async void ExecuteCreateChildEntityContextMenu(HierarchyNodeViewModel parentNode)
    {
        await CreateEntityAndStartRename(parentNode);
    }

    private async Task CreateEntityAndStartRename(HierarchyNodeViewModel? parentNode)
    {
        var entity = await _createSceneEntityCommand.CreateEntity(parent: parentNode?.Node.Content);
        if (entity == null) return;

        parentNode?.Expanded.Set(true);

        var node = _nodeCollectionController.GetAllNodes().FirstOrDefault(x => x.Node.Content == entity);
        if (node == null) return;

        _selectionHandler.ReplaceSelection(node);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            const int DELAY = 300;
            await Task.Delay(DELAY);

            node.StartRenameCommand.Execute(null);
        });
    }

    public bool DuplicateSelectedEntity()
    {
        return _selectedEntityActionService.DuplicateSelectedEntity();
    }

    public bool DeleteSelectedEntity()
    {
        return _selectedEntityActionService.DeleteSelectedEntity();
    }

    public bool RequestRenameSelectedEntity()
    {
        return _selectedEntityActionService.RequestRenameSelectedEntity();
    }

    public bool CanDropEntities(IReadOnlyList<int> draggedEntityIds, int? targetParentEntityId)
    {
        var hierarchy = _activeHierarchy;
        if (hierarchy == null) return false;

        var draggedNodes = ResolveDraggedNodes(draggedEntityIds);
        if (draggedNodes.Count == 0) return false;

        var targetParentNode = targetParentEntityId.HasValue
            ? _nodeCollectionController.FindByEntityId(targetParentEntityId.Value)?.Node
            : null;
        if (targetParentEntityId.HasValue && targetParentNode == null) return false;

        return draggedNodes.All(node => CanMoveNode(node.Node, targetParentNode));
    }

    public void MoveEntities(IReadOnlyList<int> draggedEntityIds, int? targetParentEntityId, int insertionIndex)
    {
        var hierarchy = _activeHierarchy;
        if (hierarchy == null) return;

        var draggedNodes = ResolveDraggedNodes(draggedEntityIds);
        if (draggedNodes.Count == 0) return;

        var targetParentNode = targetParentEntityId.HasValue
            ? _nodeCollectionController.FindByEntityId(targetParentEntityId.Value)?.Node
            : null;
        if (targetParentEntityId.HasValue && targetParentNode == null) return;

        if (draggedNodes.Any(node => !CanMoveNode(node.Node, targetParentNode))) return;

        if (draggedNodes.Count > 1)
        {
            var entities = draggedNodes.Select(node => node.Node.Content).ToArray();
            if (draggedNodes.All(node => node.Node.Parent == targetParentNode)) _entityManagementService.ReorderEntities(entities, insertionIndex);
            else _entityManagementService.ReparentEntities(entities, targetParentNode?.Content, insertionIndex);
            return;
        }

        var node = draggedNodes[0];
        var targetIndex = Math.Max(0, insertionIndex);
        if (node.Node.Parent == targetParentNode && hierarchy.GetNodeOrder(node.Node) < targetIndex) targetIndex--;
        _entityManagementService.SetParent(node.Node.Content, targetParentNode?.Content, targetIndex, recordHierarchyChange: true);
    }

    public void MoveEntitiesToNode(IReadOnlyList<int> draggedEntityIds, int targetParentEntityId)
    {
        var targetParentNode = _nodeCollectionController.FindByEntityId(targetParentEntityId)?.Node;
        if (targetParentNode == null) return;

        var insertionIndex = targetParentNode.ChildNodes.Count();
        MoveEntities(draggedEntityIds, targetParentEntityId, insertionIndex);
    }

    private IReadOnlyList<HierarchyNodeViewModel> GetVisibleNodes() => GetNodesInHierarchyOrder(visibleOnly: true);

    private IReadOnlyList<HierarchyNodeViewModel> GetNodesInHierarchyOrder(bool visibleOnly)
    {
        var nodes = new List<HierarchyNodeViewModel>();
        foreach (var node in Nodes.ToArray()) AppendNodes(node, nodes, visibleOnly);
        return nodes;
    }

    private static void AppendNodes(HierarchyNodeViewModel node, ICollection<HierarchyNodeViewModel> nodes, bool visibleOnly)
    {
        nodes.Add(node);
        if (visibleOnly && !node.Expanded.Value) return;
        foreach (var child in node.ChildNodes.ToArray()) AppendNodes(child, nodes, visibleOnly);
    }

    private List<HierarchyNodeViewModel> ResolveDraggedNodes(IReadOnlyList<int> draggedEntityIds)
    {
        var hierarchy = _activeHierarchy;
        if (hierarchy == null) return new List<HierarchyNodeViewModel>();

        var draggedSet = draggedEntityIds.ToHashSet();
        var draggedNodes = GetNodesInHierarchyOrder(visibleOnly: false)
            .Where(node => draggedSet.Contains(node.Node.Content.Id) && !HasDraggedAncestor(node.Node, draggedSet))
            .ToList();

        return draggedNodes;
    }

    private bool HasDraggedAncestor(HierarchyNode<GameEntity> node, IReadOnlySet<int> draggedEntityIds)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (draggedEntityIds.Contains(current.Content.Id))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static bool CanMoveNode(HierarchyNode<GameEntity> node, HierarchyNode<GameEntity>? targetParentNode)
    {
        if (node == targetParentNode) return false;

        var current = targetParentNode;
        while (current != null)
        {
            if (current == node)
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }
}
