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
using ReiEditor.Utils;
using ReiEditor.ViewModels.Windows.Editor.Project;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.Views.Windows.Editor.Project.Assets;

public partial class ProjectAssetItemView : UserControl
{
    private ProjectAssetItemViewModel? _vm;

    public ProjectAssetItemView()
    {
        InitializeComponent();
        RootBorder.AddHandler(InputElement.PointerPressedEvent, RootBorder_OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        RootBorder.AddHandler(DragDrop.DragEnterEvent, RootBorder_OnDragEnter);
        RootBorder.AddHandler(DragDrop.DropEvent, RootBorder_OnDrop);
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
        
        _vm.RequestContextMenuSelection();
        RootBorder.Focus();

        FlyoutBase.ShowAttachedFlyout(RootBorder);
        e.Handled = true;
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
            var dragPaths = GetProjectWindowViewModel()?.GetDraggedAssetPaths(_vm) ?? new[] { _vm.FullPath };
            if (dragPaths.Count == 0)
            {
                pointerDown = false;
                dragStarted = false;
                return;
            }

            var dragData = new DataObject();
            dragData.Set(DragDropDataKeys.AssetPaths, dragPaths.ToArray());
            dragData.Set(DragDropDataKeys.AssetPath, dragPaths[0]);

            try
            {
                await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move | DragDropEffects.Copy);
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
    }

    private void RootBorder_OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_vm == null || !_vm.IsDirectory)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var paths = GetDraggedAssetPaths(e);
        if (paths.Count == 0 || paths.Any(path => string.Equals(path, _vm.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void RootBorder_OnDrop(object? sender, DragEventArgs e)
    {
        if (_vm == null || !_vm.IsDirectory) return;

        var paths = GetDraggedAssetPaths(e);
        if (paths.Count == 0) return;

        var projectWindowViewModel = GetProjectWindowViewModel();
        if (projectWindowViewModel == null) return;

        await projectWindowViewModel.MoveAssetsToDirectoryAsync(paths, _vm.FullPath);
        e.Handled = true;
    }

    private ProjectWindowViewModel? GetProjectWindowViewModel()
    {
        return this.FindAncestorOfType<ProjectWindowView>()?.DataContext as ProjectWindowViewModel;
    }

    private static IReadOnlyList<string> GetDraggedAssetPaths(DragEventArgs e)
    {
        if (!e.Data.Contains(DragDropDataKeys.AssetPaths)) return Array.Empty<string>();
        if (e.Data.Get(DragDropDataKeys.AssetPaths) is not IEnumerable<string> assetPaths) return Array.Empty<string>();

        return assetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
