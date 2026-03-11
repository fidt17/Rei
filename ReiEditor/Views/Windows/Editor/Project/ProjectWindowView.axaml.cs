using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Project;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.Views.Windows.Editor.Project.Assets;

namespace ReiEditor.Views.Windows.Editor.Project;

public partial class ProjectWindowView : UserControl
{
    private ProjectWindowViewModel? _vm;

    public ProjectWindowView()
    {
        InitializeComponent();
        ActiveItemsDropTarget.AddHandler(DragDrop.DragEnterEvent, ActiveItemsDropTarget_OnDragEnter);
        ActiveItemsDropTarget.AddHandler(DragDrop.DropEvent, ActiveItemsDropTarget_OnDrop);
        AddHandler(KeyDownEvent, ProjectWindowView_OnKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += HandleDataContextChanged;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        ActiveItemsDropTarget.RemoveHandler(DragDrop.DragEnterEvent, ActiveItemsDropTarget_OnDragEnter);
        ActiveItemsDropTarget.RemoveHandler(DragDrop.DropEvent, ActiveItemsDropTarget_OnDrop);
        UnsubscribeFromContextMenu();
    }

    private void HandleDataContextChanged(object? sender, System.EventArgs e)
    {
        UnsubscribeFromContextMenu();
        _vm = DataContext as ProjectWindowViewModel;
        if (_vm == null) return;

        _vm.ActiveFolderContextMenu.AnyCommandExecutedEvent += HandleContextMenuCommandExecuted;
        _vm.ScrollToAssetRequested += HandleScrollToAssetRequested;
    }

    private void UnsubscribeFromContextMenu()
    {
        if (_vm == null) return;
        _vm.ActiveFolderContextMenu.AnyCommandExecutedEvent -= HandleContextMenuCommandExecuted;
        _vm.ScrollToAssetRequested -= HandleScrollToAssetRequested;
    }

    private void HandleContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(ActiveItemsDropTarget);
        flyout?.Hide();
    }

    private void ActiveItemsDropTarget_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ProjectWindowViewModel vm)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !IsPointerOnAssetItem(e.Source))
            {
                vm.ClearAssetSelection();
                if (sender is Control focusTarget)
                {
                    focusTarget.Focus();
                }
            }
        }

        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        if (IsPointerOnAssetItem(e.Source)) return;
        if (sender is not Control control) return;

        FlyoutBase.ShowAttachedFlyout(control);
        e.Handled = true;
    }

    private void ActiveItemsDropTarget_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not ProjectWindowViewModel vm) return;

        vm.ClearAssetSelection();
        e.Handled = true;
    }

    private void ProjectWindowView_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || e.KeyModifiers != KeyModifiers.Control) return;

        SearchFieldControl.FocusInput();
        e.Handled = true;
    }

    private void HandleScrollToAssetRequested(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return;

        Dispatcher.UIThread.Post(() =>
        {
            var targetView = ActiveItemsDropTarget
                .GetVisualDescendants()
                .OfType<ProjectAssetItemView>()
                .FirstOrDefault(view => view.DataContext is ProjectAssetItemViewModel vm &&
                                        string.Equals(vm.FullPath, assetPath, System.StringComparison.OrdinalIgnoreCase));
            if (targetView == null) return;

            targetView.BringIntoView();
        }, DispatcherPriority.Background);
    }

    private void ActiveItemsDropTarget_OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!HasFileDrop(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void ActiveItemsDropTarget_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ProjectWindowViewModel vm) return;

        var paths = GetDroppedPaths(e);
        if (paths.Count == 0) return;

        await vm.ImportExternalPathsAsync(paths);
        e.Handled = true;
    }

    private static bool HasFileDrop(DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        return files != null && files.Any();
    }

    private static List<string> GetDroppedPaths(DragEventArgs e)
    {
        var paths = new List<string>();

        var files = e.Data.GetFiles();
        if (files != null)
        {
            foreach (var item in files)
            {
                var localPath = item.Path.LocalPath;
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    paths.Add(localPath);
                }
            }
        }

        return paths.Distinct().ToList();
    }

    private static bool IsPointerOnAssetItem(object? source)
    {
        var control = source as Control;
        while (control is not null)
        {
            if (control is ProjectAssetItemView) return true;
            control = control.Parent as Control;
        }

        return false;
    }
}

