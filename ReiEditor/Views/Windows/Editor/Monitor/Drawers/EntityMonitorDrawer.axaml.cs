using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers;

public partial class EntityMonitorDrawer : UserControl
{
    private EntityMonitorDrawerViewModel? _viewModel;

    public EntityMonitorDrawer()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChangedEvent;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromViewModel();
    }

    private void HandleDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromViewModel();
        _viewModel = DataContext as EntityMonitorDrawerViewModel;
        if (_viewModel == null) return;

        _viewModel.BehaviourSelectedEvent += HandleBehaviourSelectedEvent;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_viewModel == null) return;
        _viewModel.BehaviourSelectedEvent -= HandleBehaviourSelectedEvent;
    }

    private void HandleBehaviourSelectedEvent()
    {
        AddBehaviourButton.Flyout?.Hide();
    }

    private void BehaviourSelectionFlyout_OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;

        _viewModel.SearchField.ResetSearch();
        SearchFieldControl.FocusInput();
    }
}
