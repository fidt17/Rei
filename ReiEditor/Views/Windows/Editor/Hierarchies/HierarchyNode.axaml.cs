using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyNode : UserControl
{
    private HierarchyNodeViewModel? _vm;
    
    public HierarchyNode()
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

    // ReSharper disable once UnusedParameter.Local
    private void RootBorder_OnKeyDown(object? obj, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;
        
        if (e.Key == Key.Delete)
        {
            _vm.DeleteCommand.Execute(null);
        }
        else if (e.Key == Key.F2)
        {
            _vm.StartRenameCommand.Execute(null);
        }
        else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _vm.DuplicateCommand.Execute(null);
        }
    }

    // ReSharper disable once UnusedParameter.Local
    private void NameTextBox_OnKeyDown(object? obj, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RootBorder.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            var vm = DataContext as HierarchyNodeViewModel;
            vm?.ConfirmRenameCommand.Execute(NameTextBox.Text);
            RootBorder.Focus();
        }
    }

    private void ConfigureDragAndDrop()
    {
        var target = RootBorder;
        bool pointerDown;
        
        void DoDrag(object? sender, PointerPressedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                pointerDown = true;
                if (_vm == null) return;

                if (!_vm.Selected.Value)
                {
                    await Task.Delay(100);
                    _vm.SelectCommand.Execute(null);
                }
            
                await Task.Delay(100);
                if (!pointerDown) return;
                
                var dragData = new DataObject();
                dragData.Set("Node", _vm);

                try
                {
                    await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
                }
                catch (COMException)
                {
                    // ignore
                }
            });
        }

        void Drop(object? sender, DragEventArgs e)
        {
            if (_vm == null) return;
            
            var nodeToMove = e.Data.Get("Node") as HierarchyNodeViewModel;
            if (nodeToMove == null) return;
            nodeToMove.MoveNodeCommand.Execute(new MoveNodeCommand.MoveArgs(_vm.Node, _vm.ChildNodes.Count));
            e.Handled = true;
        }

        target.PointerPressed += DoDrag;
        target.PointerReleased += (_, _) =>
        {
            pointerDown = false;
        };
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void RootBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        if (e.GetCurrentPoint(control).Properties.IsRightButtonPressed)
        {
            FlyoutBase.ShowAttachedFlyout(control);
            e.Handled = true;
            return;
        }

        if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            _vm?.SelectCommand.Execute(null);
        }
    }

    private void HandleAnyContextMenuCommandExecuted()
    {
        var flyout = FlyoutBase.GetAttachedFlyout(RootBorder);
        flyout?.Hide();
    }
}
