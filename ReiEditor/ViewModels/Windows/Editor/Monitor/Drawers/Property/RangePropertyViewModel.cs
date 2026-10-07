using System;
using System.Globalization;
using ReactiveUI;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public sealed class RangePropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    public SerializedNumericRange Range { get; }

    private double _sliderValue;
    public double SliderValue
    {
        get => _sliderValue;
        private set => SetField(ref _sliderValue, value);
    }

    private string _inputText = "";
    public string InputText
    {
        get => _inputText;
        set => SetField(ref _inputText, value);
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (!SetField(ref _statusText, value)) return;
            this.RaisePropertyChanged(nameof(HasStatus));
        }
    }
    public bool HasStatus => StatusText.Length != 0;

    private readonly SerializedProperty _property;
    private string _displayText = "";
    private bool _disposed;

    public RangePropertyViewModel(SerializedProperty property, SerializedNumericRange range)
    {
        _property = property;
        Range = range;
        PropertyName = new(property);
        _property.ValueChangedEvent += HandleValueChanged;
        RefreshDisplay();
    }

    public void SetSliderValue(double value)
    {
        if (_disposed || !double.IsFinite(value)) return;
        Commit(Range.NormalizeSlider(value));
    }

    public void ApplyInput(bool force = false)
    {
        if (_disposed) return;
        if (!force && InputText == _displayText) return;
        if (!Range.TryParseInput(InputText, out var value))
        {
            StatusText = Range.IsInteger ? "Enter an i32 integer." : "Enter a finite f32 number.";
            return;
        }
        Commit(Range.Clamp(value));
    }

    public void CancelInput()
    {
        if (_disposed) return;
        RefreshDisplay();
    }

    public override void Dispose()
    {
        _disposed = true;
        _property.ValueChangedEvent -= HandleValueChanged;
        base.Dispose();
    }

    private void Commit(double value)
    {
        // Compare numeric values, not their boxed JSON representation (long/double).
        if (Convert.ToDouble(_property.Value, CultureInfo.InvariantCulture) != Convert.ToDouble(Range.ToFieldValue(value), CultureInfo.InvariantCulture))
            _property.Value = Range.ToFieldValue(value);
        RefreshDisplay();
    }

    private void HandleValueChanged(object? value) => RefreshDisplay();

    private void RefreshDisplay()
    {
        var value = Convert.ToDouble(_property.Value, CultureInfo.InvariantCulture);
        InputText = _property.Value switch
        {
            float number => number.ToString("G9", CultureInfo.CurrentCulture),
            double number => number.ToString("G17", CultureInfo.CurrentCulture),
            IFormattable number => number.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString("G17", CultureInfo.CurrentCulture)
        };
        _displayText = InputText;
        SliderValue = double.IsFinite(value) ? Range.Clamp(value) : Range.Minimum;
        StatusText = !double.IsFinite(value) || value < Range.Minimum || value > Range.Maximum
            ? $"Stored value is outside {Range.Label}. Edit to correct it."
            : "";
    }
}
