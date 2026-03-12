using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

public class HierarchyNodeCollectionController
{
    public ObservableCollection<HierarchyNodeViewModel> Nodes { get; } = new();

    public Hierarchy<GameEntity>? ActiveHierarchy => _activeHierarchy;

    private readonly Dictionary<HierarchyNode<GameEntity>, HierarchyNodeViewModel> _nodeMap = new();
    private readonly IFactory<HierarchyNodeViewModel> _hierarchyElementFactory;
    private readonly Action<HierarchyNodeViewModel> _nodeConfiguredAction;

    private Hierarchy<GameEntity>? _activeHierarchy;

    public HierarchyNodeCollectionController(
        IFactory<HierarchyNodeViewModel> hierarchyElementFactory,
        Action<HierarchyNodeViewModel> nodeConfiguredAction)
    {
        _hierarchyElementFactory = hierarchyElementFactory;
        _nodeConfiguredAction = nodeConfiguredAction;
    }

    public void Dispose()
    {
        Reset();
    }

    public void SetHierarchy(Hierarchy<GameEntity> hierarchy, IReadOnlySet<int> expandedEntityIds)
    {
        Reset();

        _activeHierarchy = hierarchy;
        _activeHierarchy.NodeAddedEvent += HandleNodeAddedEvent;
        _activeHierarchy.NodeRemovedEvent += HandleNodeRemovedEvent;
        _activeHierarchy.NodeMovedEvent += HandleNodeMovedEvent;

        foreach (var rootNode in hierarchy.RootNodes)
        {
            HandleNodeAddedEvent(rootNode);
        }

        RestoreExpandedState(expandedEntityIds);
    }

    public HashSet<int> CaptureExpandedEntityIds()
    {
        var expandedEntityIds = new HashSet<int>();
        foreach (var node in _nodeMap.Values)
        {
            if (!node.Expanded.Value) continue;
            expandedEntityIds.Add(node.Node.Content.Id);
        }

        return expandedEntityIds;
    }

    public void RestoreExpandedState(IReadOnlySet<int> expandedEntityIds)
    {
        foreach (var node in _nodeMap.Values)
        {
            node.Expanded.Value = expandedEntityIds.Contains(node.Node.Content.Id);
        }
    }

    public IEnumerable<HierarchyNodeViewModel> GetAllNodes()
    {
        return _nodeMap.Values;
    }

    public HierarchyNodeViewModel? FindByEntityId(int entityId)
    {
        return _nodeMap.Values.FirstOrDefault(node => node.Node.Content.Id == entityId);
    }

    private void Reset()
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

    private void HandleNodeAddedEvent(HierarchyNode<GameEntity> node)
    {
        var nodeViewModel = _hierarchyElementFactory.CreateInstance(node);
        RegisterNode(nodeViewModel);
        Nodes.Add(nodeViewModel);

        foreach (var childNode in nodeViewModel.CreateChildNodes(_hierarchyElementFactory))
        {
            RegisterNode(childNode);
        }
    }

    private void RegisterNode(HierarchyNodeViewModel node)
    {
        _nodeConfiguredAction(node);
        _nodeMap[node.Node] = node;
    }

    private void HandleNodeRemovedEvent(HierarchyNode<GameEntity> node)
    {
        if (!_nodeMap.TryGetValue(node, out var targetNode)) return;

        targetNode.Dispose();
        _nodeMap.Remove(node);

        if (node.Parent == null)
        {
            Nodes.Remove(targetNode);
            return;
        }

        if (_nodeMap.TryGetValue(node.Parent, out var parent))
        {
            parent.ChildNodes.Remove(targetNode);
        }
    }

    private void HandleNodeMovedEvent(HierarchyNode<GameEntity> node, HierarchyNode<GameEntity>? oldParent, int oldOrder, int newOrder)
    {
        if (!_nodeMap.TryGetValue(node, out var nodeVm)) return;

        if (node.Parent == oldParent && oldOrder < newOrder)
        {
            newOrder -= 1;
        }

        if (oldParent == null)
        {
            Nodes.Remove(nodeVm);
        }
        else if (_nodeMap.TryGetValue(oldParent, out var oldParentNode))
        {
            oldParentNode.ChildNodes.Remove(nodeVm);
        }

        if (node.Parent == null)
        {
            Nodes.Insert(newOrder, nodeVm);
            return;
        }

        if (_nodeMap.TryGetValue(node.Parent, out var newParentNode))
        {
            newParentNode.ChildNodes.Insert(newOrder, nodeVm);
        }
    }
}
