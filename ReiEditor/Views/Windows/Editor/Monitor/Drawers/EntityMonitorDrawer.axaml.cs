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
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Monitor.BuildView");
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
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Monitor.BindView", (DataContext as EntityMonitorDrawerViewModel)?.EntityId ?? 0);
        UnsubscribeFromViewModel();
        _viewModel = DataContext as EntityMonitorDrawerViewModel;
        if (_viewModel == null) return;

        _viewModel.BehaviourSelectedEvent += HandleBehaviourSelectedEvent;
        if (ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Enabled) LayoutUpdated += HandleFirstLayout;
    }

    private void HandleFirstLayout(object? sender, EventArgs e)
    {
        LayoutUpdated -= HandleFirstLayout;
        ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Mark("Monitor.FirstLayout", _viewModel?.EntityId ?? 0);
    }

    protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
    {
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Monitor.Measure", _viewModel?.EntityId ?? 0);
        return base.MeasureOverride(availableSize);
    }

    protected override Avalonia.Size ArrangeOverride(Avalonia.Size finalSize)
    {
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Monitor.Arrange", _viewModel?.EntityId ?? 0);
        return base.ArrangeOverride(finalSize);
    }

    private void UnsubscribeFromViewModel()
    {
        LayoutUpdated -= HandleFirstLayout;
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
