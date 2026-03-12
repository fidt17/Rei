using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyWindow : UserControl
{
    private HierarchyWindowViewModel? _vm;

    public HierarchyWindow()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChangedEvent;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromVm();
    }

    private void HierarchyContentBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        if (e.GetCurrentPoint(control).Properties.IsRightButtonPressed)
        {
            FlyoutBase.ShowAttachedFlyout(control);
            e.Handled = true;
            
            return;
        }

        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<HierarchyNode>() != null)
        {
            return;
        }

        if (DataContext is not HierarchyWindowViewModel vm) return;
        var command = vm.ResetSelectionCommand;
        if (command == null) return;

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        if (DataContext is not HierarchyWindowViewModel vm) return;

        _vm = vm;
        _vm.RootContextMenu.AnyCommandExecutedEvent += HandleAnyRootContextMenuCommandExecuted;
        _vm.ScrollToEntityRequested += HandleScrollToEntityRequested;
    }

    private void UnsubscribeFromVm()
    {
        if (_vm == null) return;

        _vm.RootContextMenu.AnyCommandExecutedEvent -= HandleAnyRootContextMenuCommandExecuted;
        _vm.ScrollToEntityRequested -= HandleScrollToEntityRequested;
        _vm = null;
    }

    private void HandleAnyRootContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(RootBorder);
        flyout?.Hide();
    }

    private void HandleScrollToEntityRequested(int entityId)
    {
        if (entityId <= 0) return;

        Dispatcher.UIThread.Post(() =>
        {
            var targetNode = this.GetVisualDescendants()
                .OfType<HierarchyNode>()
                .FirstOrDefault(view => view.DataContext is HierarchyNodeViewModel vm &&
                                        vm.Node.Content.Id == entityId);
            if (targetNode == null) return;

            RootBorder.Focus();
            targetNode.BringIntoView();
        }, DispatcherPriority.Background);
    }
}
