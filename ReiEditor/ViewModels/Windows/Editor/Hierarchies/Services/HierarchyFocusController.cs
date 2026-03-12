using System;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.ViewModels.Windows.Editor.Hierarchies.Services;

public class HierarchyFocusController
{
    private readonly Func<int, HierarchyNodeViewModel?> _findNodeByEntityId;
    private readonly Action<HierarchyNodeViewModel> _replaceSelectionAction;
    private readonly Action<int> _scrollToEntityAction;

    public HierarchyFocusController(
        Func<int, HierarchyNodeViewModel?> findNodeByEntityId,
        Action<HierarchyNodeViewModel> replaceSelectionAction,
        Action<int> scrollToEntityAction)
    {
        _findNodeByEntityId = findNodeByEntityId;
        _replaceSelectionAction = replaceSelectionAction;
        _scrollToEntityAction = scrollToEntityAction;
    }

    public void HandleRenameEntityRequested(int entityId)
    {
        var targetNode = _findNodeByEntityId(entityId);
        if (targetNode == null) return;

        ExpandAncestors(targetNode);
        _replaceSelectionAction(targetNode);
        Dispatcher.UIThread.InvokeAsync(() => targetNode.StartRenameCommand.Execute(null));
    }

    public void HandleActiveSelectionChanged(ISelectable? selection)
    {
        if (selection is not IEntitySelectable entitySelection) return;

        var targetNode = _findNodeByEntityId(entitySelection.Entity.Id);
        if (targetNode == null) return;

        ExpandAncestors(targetNode);
        _scrollToEntityAction(entitySelection.Entity.Id);
    }

    private void ExpandAncestors(HierarchyNodeViewModel node)
    {
        var current = node.Node.Parent;
        while (current != null)
        {
            var currentNode = _findNodeByEntityId(current.Content.Id);
            if (currentNode != null)
            {
                currentNode.Expanded.Value = true;
            }

            current = current.Parent;
        }
    }
}
