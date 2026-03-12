using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

public class HierarchySelectionHandler
{
    private readonly ISelectionService _selectionService;
    private readonly Func<IReadOnlyCollection<HierarchyNodeViewModel>> _allNodesProvider;
    private readonly Func<IReadOnlyList<HierarchyNodeViewModel>> _visibleNodesProvider;

    private readonly HashSet<int> _selectedEntityIds = new();
    private int? _primarySelectedEntityId;
    private int? _selectionAnchorEntityId;

    public HierarchySelectionHandler(
        ISelectionService selectionService,
        Func<IReadOnlyCollection<HierarchyNodeViewModel>> allNodesProvider,
        Func<IReadOnlyList<HierarchyNodeViewModel>> visibleNodesProvider)
    {
        _selectionService = selectionService;
        _allNodesProvider = allNodesProvider;
        _visibleNodesProvider = visibleNodesProvider;
    }

    public void HandleSelectionChanged(IReadOnlyCollection<ISelectable> selection)
    {
        var selectedEntities = selection
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)
            .ToHashSet();

        _selectedEntityIds.Clear();
        foreach (var entityId in selectedEntities)
        {
            _selectedEntityIds.Add(entityId);
        }

        _primarySelectedEntityId = _selectionService.ActiveSelection.Value is IEntitySelectable entitySelection
            ? entitySelection.Entity.Id
            : selectedEntities.Count > 0
                ? selectedEntities.First()
                : null;

        if (_selectedEntityIds.Count == 0)
        {
            _selectionAnchorEntityId = null;
        }
        else if (!_selectionAnchorEntityId.HasValue || !_selectedEntityIds.Contains(_selectionAnchorEntityId.Value))
        {
            _selectionAnchorEntityId = _primarySelectedEntityId;
        }

        UpdateNodeSelectionState();
    }

    public void HandleNodeSelectionRequested(HierarchyNodeViewModel node, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            SelectNodeRange(node, modifiers.HasFlag(KeyModifiers.Control));
            return;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            ToggleSelection(node);
            return;
        }

        ReplaceSelection(node);
    }

    public void HandleNodeContextMenuSelectionRequested(HierarchyNodeViewModel node)
    {
        if (_selectedEntityIds.Contains(node.Node.Content.Id))
        {
            SetPrimarySelection(node);
            return;
        }

        ReplaceSelection(node);
    }

    public void ReplaceSelection(HierarchyNodeViewModel node)
    {
        _selectedEntityIds.Clear();
        _selectedEntityIds.Add(node.Node.Content.Id);
        _primarySelectedEntityId = node.Node.Content.Id;
        _selectionAnchorEntityId = node.Node.Content.Id;
        ApplySelection();
    }

    public void RestoreSelection()
    {
        if (_selectedEntityIds.Count == 0)
        {
            UpdateNodeSelectionState();
            return;
        }

        ApplySelection(sendToEngine: false);
    }

    public void ResetSelection()
    {
        _selectionService.ResetSelection();
    }

    private void ToggleSelection(HierarchyNodeViewModel node)
    {
        var entityId = node.Node.Content.Id;
        if (_selectedEntityIds.Contains(entityId))
        {
            _selectedEntityIds.Remove(entityId);
            if (_primarySelectedEntityId == entityId)
            {
                _primarySelectedEntityId = _visibleNodesProvider()
                    .FirstOrDefault(item => _selectedEntityIds.Contains(item.Node.Content.Id))
                    ?.Node.Content.Id;
            }
        }
        else
        {
            _selectedEntityIds.Add(entityId);
            _primarySelectedEntityId = entityId;
        }

        _selectionAnchorEntityId = entityId;
        ApplySelection();
    }

    private void SelectNodeRange(HierarchyNodeViewModel node, bool addToExistingSelection)
    {
        var visibleNodes = _visibleNodesProvider();
        var targetIndex = -1;
        for (var i = 0; i < visibleNodes.Count; i++)
        {
            if (!ReferenceEquals(visibleNodes[i], node)) continue;
            targetIndex = i;
            break;
        }

        if (targetIndex < 0)
        {
            ReplaceSelection(node);
            return;
        }

        var anchorIndex = GetAnchorIndex(visibleNodes, targetIndex);
        if (!addToExistingSelection)
        {
            _selectedEntityIds.Clear();
        }

        var start = System.Math.Min(anchorIndex, targetIndex);
        var end = System.Math.Max(anchorIndex, targetIndex);
        for (var i = start; i <= end; i++)
        {
            _selectedEntityIds.Add(visibleNodes[i].Node.Content.Id);
        }

        _primarySelectedEntityId = node.Node.Content.Id;
        ApplySelection();
    }

    private int GetAnchorIndex(IReadOnlyList<HierarchyNodeViewModel> visibleNodes, int targetIndex)
    {
        if (!_selectionAnchorEntityId.HasValue)
        {
            _selectionAnchorEntityId = visibleNodes[targetIndex].Node.Content.Id;
            return targetIndex;
        }

        for (var i = 0; i < visibleNodes.Count; i++)
        {
            if (visibleNodes[i].Node.Content.Id != _selectionAnchorEntityId.Value) continue;
            return i;
        }

        _selectionAnchorEntityId = visibleNodes[targetIndex].Node.Content.Id;
        return targetIndex;
    }

    private void SetPrimarySelection(HierarchyNodeViewModel node)
    {
        _primarySelectedEntityId = node.Node.Content.Id;
        ApplySelection();
    }

    private void ApplySelection(bool sendToEngine = true)
    {
        var allNodes = _allNodesProvider().ToList();
        var selectedNodes = allNodes
            .Where(node => _selectedEntityIds.Contains(node.Node.Content.Id))
            .Cast<ISelectable>()
            .ToList();
        var primaryNode = allNodes.FirstOrDefault(node => node.Node.Content.Id == _primarySelectedEntityId);

        if (selectedNodes.Count == 0 || primaryNode == null)
        {
            _selectionService.ResetSelection(sendToEngine);
            return;
        }

        _selectionService.SetSelection(selectedNodes, primaryNode, sendToEngine);
    }

    private void UpdateNodeSelectionState()
    {
        foreach (var node in _allNodesProvider().ToList())
        {
            node.SetSelected(_selectedEntityIds.Contains(node.Node.Content.Id));
        }
    }
}
