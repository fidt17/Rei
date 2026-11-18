using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

public partial class RangePropertyView : UserControl
{
    private readonly Slider _slider;
    private RangePropertyViewModel? _viewModel;
    private bool _updatingSlider;
    private bool _draggingSlider;

    public RangePropertyView()
    {
        AvaloniaXamlLoader.Load(this);
        _slider = this.FindControl<Slider>("RangeSlider")!;
        _slider.PropertyChanged += SliderPropertyChanged;
        _slider.AddHandler(Thumb.DragStartedEvent, SliderDragStarted, handledEventsToo: true);
        _slider.AddHandler(Thumb.DragCompletedEvent, SliderDragCompleted, handledEventsToo: true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        BindViewModel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BindViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _viewModel = null;
        _draggingSlider = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void BindViewModel()
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _draggingSlider = false;
        _viewModel = DataContext as RangePropertyViewModel;
        if (_viewModel == null || _slider == null) return;
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        UpdateSlider();
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RangePropertyViewModel.SliderValue)) UpdateSlider();
    }

    private void UpdateSlider()
    {
        if (_viewModel == null) return;
        _updatingSlider = true;
        try
        {
            var range = _viewModel.Range;
            _slider.Minimum = range.Minimum;
            _slider.Maximum = range.Maximum;
            _slider.SmallChange = range.SmallChange;
            _slider.LargeChange = range.LargeChange;
            // Snapping lives in the VM so an irregular upper endpoint stays reachable.
            _slider.IsSnapToTickEnabled = false;
            _slider.Value = _viewModel.SliderValue;
        }
        finally
        {
            _updatingSlider = false;
        }
    }

    private void SliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_updatingSlider || _draggingSlider || e.Property != Slider.ValueProperty || _viewModel == null) return;
        _viewModel.SetSliderValue(_slider.Value);
        UpdateSlider();
    }

    private void SliderDragStarted(object? sender, VectorEventArgs e) => _draggingSlider = _viewModel?.RecordsConfirmedEdits == true;

    private void SliderDragCompleted(object? sender, VectorEventArgs e)
    {
        if (!_draggingSlider) return;
        _draggingSlider = false;
        _viewModel?.SetSliderValue(_slider.Value);
        UpdateSlider();
    }

    private void InputLostFocus(object? sender, RoutedEventArgs e) => _viewModel?.ApplyInput();

    private void InputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _viewModel?.ApplyInput(force: true);
        else if (e.Key == Key.Escape) _viewModel?.CancelInput();
        else return;
        e.Handled = true;
    }
}
