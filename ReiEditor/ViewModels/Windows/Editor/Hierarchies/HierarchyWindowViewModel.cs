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
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies;

public class HierarchyWindowViewModel : BaseViewModel
{
    public ICommand ResetSelectionCommand { get; }
	
    public ObservableField<string> SceneName { get; } = new("Scene Name");
    public ObservableCollection<HierarchyNodeViewModel> Nodes { get; } = new();

    public ContextMenuViewModel RootContextMenu { get; } = new();

    private readonly Dictionary<HierarchyNode<GameEntity>, HierarchyNodeViewModel> _nodeMap = new();

    private Hierarchy<GameEntity>? _activeHierarchy;
    private readonly ISelectionService _selectionService;
    private readonly IFactory<HierarchyNodeViewModel> _hierarchyElementFactory;
    private readonly CreateSceneEntityCommand _createSceneEntityCommand;
    private readonly ISelectedEntityEditorActionService _selectedEntityEditorActionService;

#pragma warning disable CS8618
    public HierarchyWindowViewModel() { }
#pragma warning restore CS8618

    public HierarchyWindowViewModel(
        Hierarchy<GameEntity> hierarchy,
        ISelectionService selectionService,
        IFactory<HierarchyNodeViewModel> hierarchyElementFactory,
        IFactory<CreateSceneEntityCommand> createSceneEntityCommand,
        ISelectedEntityEditorActionService selectedEntityEditorActionService)
    {
        _activeHierarchy = hierarchy;
        _selectionService = selectionService;
        _hierarchyElementFactory = hierarchyElementFactory;
        _createSceneEntityCommand = createSceneEntityCommand.CreateInstance();
        _selectedEntityEditorActionService = selectedEntityEditorActionService;
        
        SetHierarchy(hierarchy);

        ResetSelectionCommand = ReactiveCommand.Create(ResetSelection);
        RootContextMenu.AddOption(new ContextMenuOption("New Entity", ExecuteCreateNewEntityContextMenu));
        _selectedEntityEditorActionService.RenameEntityRequested += HandleRenameEntityRequestedEvent;
    }

    public override void Dispose()
    {
        _createSceneEntityCommand.Dispose();
        _selectedEntityEditorActionService.RenameEntityRequested -= HandleRenameEntityRequestedEvent;

        if (_activeHierarchy != null)
        {
            _activeHierarchy.NodeAddedEvent -= HandleNodeAddedEvent;
            _activeHierarchy.NodeRemovedEvent -= HandleNodeRemovedEvent;
            _activeHierarchy.NodeMovedEvent -= HandleNodeMovedEvent;
        }
    }

    public void SetHierarchy(Hierarchy<GameEntity> hierarchy)
    {
        var expandedEntityIds = CaptureExpandedEntityIds();
        
        ResetHierarchy();
        
        _activeHierarchy = hierarchy;
        _activeHierarchy.NodeAddedEvent += HandleNodeAddedEvent;
        _activeHierarchy.NodeRemovedEvent += HandleNodeRemovedEvent;
        _activeHierarchy.NodeMovedEvent += HandleNodeMovedEvent;
        
        SceneName.Set(hierarchy.Name);
        UpdateEntitiesList(_activeHierarchy);
        RestoreExpandedState(expandedEntityIds);
    }

    private void ResetHierarchy()
    {
        Nodes.ClearAndDispose();
        _nodeMap.Clear();

        if (_activeHierarchy != null)
        {
            _activeHierarchy.NodeAddedEvent -= HandleNodeAddedEvent;
            _activeHierarchy.NodeRemovedEvent -= HandleNodeRemovedEvent;
            _activeHierarchy.NodeMovedEvent -= HandleNodeMovedEvent;
        }

        _activeHierarchy = null;
    }

    private void UpdateEntitiesList(Hierarchy<GameEntity> h)
    {
        Nodes.ClearAndDispose();
		
        foreach (var n in h.RootNodes)
        {
            HandleNodeAddedEvent(n);
        }
    }

    private void ResetSelection() => _selectionService.ResetSelection();

    private IEnumerable<HierarchyNodeViewModel> GetAllNodes() => _nodeMap.Values;
    
    private void HandleNodeAddedEvent(HierarchyNode<GameEntity> n)
    {
        var node = _hierarchyElementFactory.CreateInstance(n);
        _nodeMap.Add(n, node);
        Nodes.Add(node);
			
        foreach (var childNode in node.CreateChildNodes(_hierarchyElementFactory))
        {
            _nodeMap.Add(childNode.Node, childNode);
        }
    }

    private void HandleNodeRemovedEvent(HierarchyNode<GameEntity> n)
    {
        if (n.Parent == null)
        {
            var targetNode = Nodes.FirstOrDefault(x => x.Node == n);
            if (targetNode == null) return;
            
            targetNode.Dispose();
            _nodeMap.Remove(n);
            
            Nodes.Remove(targetNode);
        }
        else if (_nodeMap.ContainsKey(n))
        {
            var targetNode = _nodeMap[n];
            
            targetNode.Dispose();
            _nodeMap.Remove(n);

            if (_nodeMap.ContainsKey(n.Parent))
            {
                var parent = _nodeMap[n.Parent];
                parent.ChildNodes.Remove(targetNode);
            }
        }
    }

    private void HandleNodeMovedEvent(HierarchyNode<GameEntity> node, HierarchyNode<GameEntity>? oldParent, int oldOrder, int newOrder)
    {
        var nodeVm = _nodeMap[node];
        
        if (node.Parent == oldParent)
        {
            if (oldOrder < newOrder)
            {
                newOrder -= 1;
            }
        }
        
        if (oldParent == null)
        {
            Nodes.Remove(nodeVm);
        }
        else
        {
            var parent = _nodeMap[oldParent];
            parent.ChildNodes.Remove(nodeVm);
        }

        if (node.Parent == null)
        {
            Nodes.Insert(newOrder, nodeVm);
        }
        else
        {
            var parent = _nodeMap[node.Parent];
            parent.ChildNodes.Insert(newOrder, nodeVm);
        }
    }

    private void ExecuteCreateNewEntityContextMenu()
    {
        var e = _createSceneEntityCommand.CreateEntity();
        if (e == null) return;

        var node = GetAllNodes().FirstOrDefault(x => x.Node.Content == e);
        if (node == null) return;
        
        node.Select();
        
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            const int DELAY = 300;
            await Task.Delay(DELAY);
                    
            node.StartRenameCommand.Execute(null);
        });
    }

    private HashSet<int> CaptureExpandedEntityIds()
    {
        var expandedEntityIds = new HashSet<int>();
        foreach (var node in _nodeMap.Values)
        {
            if (!node.Expanded.Value) continue;
            expandedEntityIds.Add(node.Node.Content.Id);
        }

        return expandedEntityIds;
    }

    private void RestoreExpandedState(IReadOnlySet<int> expandedEntityIds)
    {
        foreach (var node in _nodeMap.Values)
        {
            node.Expanded.Value = expandedEntityIds.Contains(node.Node.Content.Id);
        }
    }

    private void HandleRenameEntityRequestedEvent(int entityId)
    {
        var targetNode = _nodeMap.Values.FirstOrDefault(node => node.Node.Content.Id == entityId);
        if (targetNode == null) return;

        ExpandAncestors(targetNode);
        Dispatcher.UIThread.InvokeAsync(() => targetNode.StartRenameCommand.Execute(null));
    }

    private void ExpandAncestors(HierarchyNodeViewModel node)
    {
        var current = node.Node.Parent;
        while (current != null)
        {
            if (_nodeMap.TryGetValue(current, out var currentNodeVm))
            {
                currentNodeVm.Expanded.Value = true;
            }

            current = current.Parent;
        }
    }
}
