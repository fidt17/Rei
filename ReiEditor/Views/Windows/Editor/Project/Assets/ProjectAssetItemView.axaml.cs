using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.Views.Windows.Editor.Project.Assets;

public partial class ProjectAssetItemView : UserControl
{
    private ProjectAssetItemViewModel? _vm;

    public ProjectAssetItemView()
    {
        InitializeComponent();
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
        _vm.CombinedContextMenu.AnyCommandExecutedEvent -= HandleContextMenuCommandExecuted;
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        _vm = DataContext as ProjectAssetItemViewModel;
        if (_vm == null) return;

        _vm.StartRenameCommand.ExecutedEvent += EnableNameTextBox;
        _vm.CombinedContextMenu.AnyCommandExecutedEvent += HandleContextMenuCommandExecuted;
    }

    private void HandleContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(RootBorder);
        flyout?.Hide();
    }

    private void EnableNameTextBox()
    {
        NameTextBox.IsVisible = true;
        NameTextBox.Focus(NavigationMethod.Pointer);
        NameTextBox.SelectAll();
    }

    private void RootBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        
        _vm.SelectCommand.Execute(null);
        RootBorder.Focus();

        FlyoutBase.ShowAttachedFlyout(RootBorder);
        e.Handled = true;
    }

    private void RootBorder_OnTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;

        _vm.SelectCommand.Execute(null);
        RootBorder.Focus();
    }

    private void RootBorder_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;

        if (e.Key == Key.F2)
        {
            _vm.StartRenameCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            _vm.DeleteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.DuplicateCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RootBorder_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;

        _vm.OpenCommand.Execute(null);
        e.Handled = true;
    }

    private void NameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            NameTextBox.IsVisible = false;
            RootBorder.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            if (_vm != null)
            {
                _vm.RenameValue.Value = NameTextBox.Text ?? "";
            }
            _vm?.ConfirmRenameCommand.Execute(null);
            NameTextBox.IsVisible = false;
            RootBorder.Focus();
        }
    }

    private void NameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        NameTextBox.IsVisible = false;
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
            if (_vm.IsDirectory) return;
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

            dragStarted = true;
            var dragData = new DataObject();
            dragData.Set(DragDropDataKeys.AssetPath, _vm.FullPath);

            try
            {
                await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
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

            vm!.SelectCommand.Execute(null);
            RootBorder.Focus();
        };
    }
}
