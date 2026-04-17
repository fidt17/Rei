using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.Utils;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyNode : UserControl
{
    private HierarchyNodeViewModel? _vm;

    public HierarchyNode()
    {
        InitializeComponent();

        RootBorder.AddHandler(PointerPressedEvent, RootBorder_OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += HandleDataContextChangedEvent;

        ConfigureDragAndDrop();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromVm();
    }

    private void UnsubscribeFromVm()
    {
        if (_vm == null) return;
        _vm.StartRenameCommand.ExecutedEvent -= EnableNameTextBox;
        _vm.ContextMenu.AnyCommandExecutedEvent -= HandleAnyContextMenuCommandExecuted;
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        if (DataContext is not HierarchyNodeViewModel vm) return;
        _vm = vm;

        _vm.StartRenameCommand.ExecutedEvent += EnableNameTextBox;
        _vm.ContextMenu.AnyCommandExecutedEvent += HandleAnyContextMenuCommandExecuted;
    }

    private void EnableNameTextBox()
    {
        NameTextBox.IsVisible = true;
        NameTextBox.Focus(NavigationMethod.Pointer);
        NameTextBox.SelectAll();
    }

    private void RootBorder_OnKeyDown(object? obj, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;

        if (e.Key == Key.Delete)
        {
            _vm.DeleteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            _vm.StartRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.DuplicateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void NameTextBox_OnKeyDown(object? obj, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            NameTextBox.IsVisible = false;
            RootBorder.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            _vm?.ConfirmRenameCommand.Execute(NameTextBox.Text);
            NameTextBox.IsVisible = false;
            RootBorder.Focus();
        }
    }

    private void ConfigureDragAndDrop()
    {
        var target = RootBorder;
        var pointerDown = false;
        var dragStarted = false;
        Point pressedPosition = default;
        const double DRAG_THRESHOLD = 4;

        target.PointerPressed += (_, e) =>
        {
            if (_vm == null) return;
            if (!e.GetCurrentPoint(target).Properties.IsLeftButtonPressed) return;

            pointerDown = true;
            dragStarted = false;
            pressedPosition = e.GetPosition(target);
        };

        target.PointerMoved += async (_, e) =>
        {
            if (_vm == null) return;
            if (!pointerDown || dragStarted) return;
            if (!e.GetCurrentPoint(target).Properties.IsLeftButtonPressed) return;

            var currentPosition = e.GetPosition(target);
            var delta = currentPosition - pressedPosition;
            var movedEnough = Math.Abs(delta.X) >= DRAG_THRESHOLD || Math.Abs(delta.Y) >= DRAG_THRESHOLD;
            if (!movedEnough) return;

            if (!_vm.Selected.Value)
            {
                _vm.RequestSelection(KeyModifiers.None);
            }

            dragStarted = true;
            var draggedEntityIds = _vm.GetDraggedEntityIds();
            if (draggedEntityIds.Count == 0)
            {
                pointerDown = false;
                dragStarted = false;
                return;
            }

            var dragData = new DataObject();
            dragData.Set(DragDropDataKeys.EntityIds, draggedEntityIds.ToArray());
            dragData.Set(DragDropDataKeys.EntityId, draggedEntityIds[0]);

            try
            {
                await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
            }
            catch (COMException)
            {
            }
            finally
            {
                pointerDown = false;
            }
        };

        target.PointerReleased += (_, e) =>
        {
            var vm = _vm;
            var shouldSelect = pointerDown && !dragStarted && vm != null && e.InitialPressMouseButton == MouseButton.Left;
            pointerDown = false;
            dragStarted = false;

            if (!shouldSelect) return;

            vm!.RequestSelection(e.KeyModifiers);
            RootBorder.Focus();
        };

        target.AddHandler(DragDrop.DragEnterEvent, RootBorder_OnDragEnter);
        target.AddHandler(DragDrop.DropEvent, RootBorder_OnDrop);
    }

    private void RootBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;

        _vm.RequestContextMenuSelection();
        RootBorder.Focus();
        FlyoutBase.ShowAttachedFlyout(RootBorder);
        e.Handled = true;
    }

    private void HandleAnyContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(RootBorder);
        flyout?.Hide();
    }

    private void RootBorder_OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_vm == null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var draggedEntityIds = GetDraggedEntityIds(e);
        var hierarchyWindowVm = GetHierarchyWindowViewModel();
        if (draggedEntityIds.Count == 0 || hierarchyWindowVm == null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = hierarchyWindowVm.CanDropEntities(draggedEntityIds, _vm.Node.Content.Id)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void RootBorder_OnDrop(object? sender, DragEventArgs e)
    {
        if (_vm == null) return;

        var draggedEntityIds = GetDraggedEntityIds(e);
        var hierarchyWindowVm = GetHierarchyWindowViewModel();
        if (draggedEntityIds.Count == 0 || hierarchyWindowVm == null) return;

        hierarchyWindowVm.MoveEntitiesToNode(draggedEntityIds, _vm.Node.Content.Id);
        e.Handled = true;
    }

    private HierarchyWindowViewModel? GetHierarchyWindowViewModel()
    {
        return this.FindAncestorOfType<HierarchyWindow>()?.DataContext as HierarchyWindowViewModel;
    }

    private static IReadOnlyList<int> GetDraggedEntityIds(DragEventArgs e)
    {
        if (!e.Data.Contains(DragDropDataKeys.EntityIds)) return Array.Empty<int>();
        if (e.Data.Get(DragDropDataKeys.EntityIds) is not IEnumerable<int> entityIds) return Array.Empty<int>();

        return entityIds.Distinct().ToArray();
    }
}
