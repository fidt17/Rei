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

        var targetParentEntity = targetParentNode?.Content;
        var targetIndex = Math.Max(0, insertionIndex);

        foreach (var node in draggedNodes)
        {
            var currentParent = node.Node.Parent;
            var currentIndex = hierarchy.GetNodeOrder(node.Node);
            if (currentParent == targetParentNode && currentIndex < targetIndex)
            {
                targetIndex -= 1;
            }

            _entityManagementService.SetParent(node.Node.Content, targetParentEntity, targetIndex);
            targetIndex += 1;
        }
    }

    public void MoveEntitiesToNode(IReadOnlyList<int> draggedEntityIds, int targetParentEntityId)
    {
        var targetParentNode = _nodeCollectionController.FindByEntityId(targetParentEntityId)?.Node;
        if (targetParentNode == null) return;

        var insertionIndex = targetParentNode.ChildNodes.Count();
        MoveEntities(draggedEntityIds, targetParentEntityId, insertionIndex);
    }

    private IReadOnlyList<HierarchyNodeViewModel> GetVisibleNodes()
    {
        var visibleNodes = new List<HierarchyNodeViewModel>();
        foreach (var node in Nodes.ToArray())
        {
            AppendVisibleNode(node, visibleNodes);
        }

        return visibleNodes;
    }

    private static void AppendVisibleNode(HierarchyNodeViewModel node, ICollection<HierarchyNodeViewModel> visibleNodes)
    {
        visibleNodes.Add(node);
        if (!node.Expanded.Value) return;

        foreach (var childNode in node.ChildNodes.ToArray())
        {
            AppendVisibleNode(childNode, visibleNodes);
        }
    }

    private List<HierarchyNodeViewModel> ResolveDraggedNodes(IReadOnlyList<int> draggedEntityIds)
    {
        var hierarchy = _activeHierarchy;
        if (hierarchy == null) return new List<HierarchyNodeViewModel>();

        var draggedSet = draggedEntityIds.ToHashSet();
        var draggedNodes = draggedSet
            .Select(id => _nodeCollectionController.FindByEntityId(id))
            .Where(node => node != null)
            .Cast<HierarchyNodeViewModel>()
            .Where(node => !HasDraggedAncestor(node.Node, draggedSet))
            .OrderBy(node => hierarchy.GetNodeOrder(node.Node))
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
