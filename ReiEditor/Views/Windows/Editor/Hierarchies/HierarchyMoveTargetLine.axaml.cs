using Avalonia;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

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
        var nodeToMove = e.Data.Get("Node") as HierarchyNodeViewModel;
        if (nodeToMove == NodeData)
        {
            IsOver = false;
            return;
        }

        var nodeIndex = GetNodeIndexInParent(NodeData);
        if (IsTopLine && nodeIndex != 0)
        {
            IsOver = false;
            return;
        }
        
        IsOver = true;
    }

    private void HandleDropEvent(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        IsOver = false;
        
        var nodeToMove = e.Data.Get("Node") as HierarchyNodeViewModel;
        if (nodeToMove == null) return;

        var thisNode = NodeData.Node;
        var thisNodeParent = thisNode.Parent;
        var targetIndex = GetNodeIndexInParent(NodeData);
        var moveIdx = targetIndex + (IsTopLine ? 0 : 1);
        var sourceIndex = GetNodeIndexInParent(nodeToMove);

        if (nodeToMove.Node.Parent == thisNodeParent && sourceIndex < moveIdx)
        {
            moveIdx -= 1;
        }

        nodeToMove.MoveNodeCommand.Execute(new MoveNodeCommand.MoveArgs(thisNodeParent, moveIdx));
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
}
