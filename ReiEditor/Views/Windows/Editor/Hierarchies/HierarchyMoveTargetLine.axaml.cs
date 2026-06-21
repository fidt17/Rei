using System;
using Avalonia;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.Utils;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyMoveTargetLine : UserControl
{
    public static readonly StyledProperty<bool> IsOverProperty = AvaloniaProperty.Register<HierarchyMoveTargetLine, bool>(
        "IsOver");

    public bool IsOver
    {
        get => GetValue(IsOverProperty);
        set => SetValue(IsOverProperty, value);
    }

    public static readonly StyledProperty<HierarchyNodeViewModel> NodeDataProperty = AvaloniaProperty.Register<HierarchyMoveTargetLine, HierarchyNodeViewModel>(
        "NodeData");

    public static readonly StyledProperty<bool> IsTopLineProperty = AvaloniaProperty.Register<HierarchyMoveTargetLine, bool>(
        "IsTopLine");

    public bool IsTopLine
    {
        get => GetValue(IsTopLineProperty);
        set => SetValue(IsTopLineProperty, value);
    }
    
    public HierarchyNodeViewModel NodeData
    {
        get => GetValue(NodeDataProperty);
        set => SetValue(NodeDataProperty, value);
    }
    
    public HierarchyMoveTargetLine()
    {
        InitializeComponent();
        
        AddHandler(DragDrop.DragEnterEvent, HandleDragEnterEvent);
        AddHandler(DragDrop.DropEvent, HandleDropEvent);
        AddHandler(DragDrop.DragLeaveEvent, HandleDragLeaveEvent);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        RemoveHandler(DragDrop.DragEnterEvent, HandleDragEnterEvent);
        RemoveHandler(DragDrop.DropEvent, HandleDropEvent);
        RemoveHandler(DragDrop.DragLeaveEvent, HandleDragLeaveEvent);
    }

    private void HandleDragEnterEvent(object? sender, DragEventArgs e)
    {
        var draggedEntityIds = GetDraggedEntityIds(e);
        if (draggedEntityIds.Count == 0)
        {
            IsOver = false;
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var hierarchyWindowVm = GetHierarchyWindowViewModel();
        if (hierarchyWindowVm == null)
        {
            IsOver = false;
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var nodeIndex = GetNodeIndexInParent(NodeData);
        if (IsTopLine && nodeIndex != 0)
        {
            IsOver = false;
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var targetParentEntityId = NodeData.Node.Parent?.Content.Id;
        var canDrop = hierarchyWindowVm.CanDropEntities(draggedEntityIds, targetParentEntityId);
        IsOver = canDrop;
        e.DragEffects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void HandleDropEvent(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        IsOver = false;
        
        var draggedEntityIds = GetDraggedEntityIds(e);
        var hierarchyWindowVm = GetHierarchyWindowViewModel();
        if (draggedEntityIds.Count == 0 || hierarchyWindowVm == null) return;

        var thisNode = NodeData.Node;
        var thisNodeParent = thisNode.Parent;
        var targetIndex = GetNodeIndexInParent(NodeData);
        var moveIdx = targetIndex + (IsTopLine ? 0 : 1);
        hierarchyWindowVm.MoveEntities(draggedEntityIds, thisNodeParent?.Content.Id, moveIdx);
    }

    private void HandleDragLeaveEvent(object? sender, DragEventArgs e) => IsOver = false;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private int GetNodeIndexInParent(HierarchyNodeViewModel node)
    {
        var parent = node.Node.Parent;
        if (parent != null)
        {
            return parent.GetChildIdx(node.Node);
        }

        var hierarchyWindowVm = GetHierarchyWindowViewModel();
        if (hierarchyWindowVm != null)
        {
            var rootIndex = hierarchyWindowVm.Nodes.IndexOf(node);
            if (rootIndex >= 0)
            {
                return rootIndex;
            }
        }

        return node.Node.Content.Transform.Order;
    }

    private HierarchyWindowViewModel? GetHierarchyWindowViewModel()
    {
        var hierarchyWindow = this.GetVisualAncestors().OfType<HierarchyWindow>().FirstOrDefault();
        return hierarchyWindow?.DataContext as HierarchyWindowViewModel;
    }

    private static IReadOnlyList<int> GetDraggedEntityIds(DragEventArgs e)
    {
        if (!e.Data.Contains(DragDropDataKeys.EntityIds)) return Array.Empty<int>();
        if (e.Data.Get(DragDropDataKeys.EntityIds) is not IEnumerable<int> entityIds) return Array.Empty<int>();

        return entityIds.Distinct().ToArray();
    }
}
