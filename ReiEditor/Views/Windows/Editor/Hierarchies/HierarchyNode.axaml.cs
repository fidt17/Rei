using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReiEditor.Models.Resources;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.Views.Windows.Editor.Hierarchies;

public partial class HierarchyNode : UserControl
{
    private const string DragOverClass = "DragOver";
    
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
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromVm();
        if (DataContext is not HierarchyNodeViewModel vm) return;
        _vm = vm;

        _vm.StartRenameCommand.ExecutedEvent += EnableNameTextBox;
    }

    private void EnableNameTextBox()
    {
        NameTextBox.IsVisible = true;
        NameTextBox.Focus(NavigationMethod.Pointer);
        NameTextBox.SelectAll();
    }

    private void RootBorder_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;
        if (e.Key != Key.Delete) return;
		
        _vm.DeleteCommand.Execute(null);
    }

    private void InputElement_OnTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;
        if (!_vm.Selected.Value) return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            const int DELAY = 300;
            await Task.Delay(DELAY);
            
            if (!_vm.Selected.Value) return;
            _vm.StartRenameCommand.Execute(null);
        });
    }

    private void NameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
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
        var pointerDown = false;
        
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
                catch (COMException exception)
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
        target.PointerReleased += (_, __) =>
        {
            pointerDown = false;
        };
        AddHandler(DragDrop.DropEvent, Drop);
    }
}