using System;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class ColorPropertyViewModel : BaseCustomPropertyViewModel
{
    #region R

    private float _r;
    public float R
    {
        get => _r;
        set
        {
            if (SetField(ref _r, value))
            {
                var property = GetNestedProperty("r");
                if (property != null)
                {
                    property.Value = value;
                }
                UpdateColorHex();
            }
        }
    }

    #endregion
    
    #region G

    private float _g;
    public float G
    {
        get => _g;
        set
        {
            if (SetField(ref _g, value))
            {
                var property = GetNestedProperty("g");
                if (property != null)
                {
                    property.Value = value;
                }
                UpdateColorHex();
            }
        }
    }

    #endregion
    
    #region B

    private float _b;
    public float B
    {
        get => _b;
        set
        {
            if (SetField(ref _b, value))
            {
                var property = GetNestedProperty("b");
                if (property != null)
                {
                    property.Value = value;
                }
                UpdateColorHex();
            }
        }
    }

    #endregion
    
    #region A

    private float _a;
    public float A
    {
        get => _a;
        set
        {
            if (SetField(ref _a, value))
            {
                var property = GetNestedProperty("a");
                if (property != null)
                {
                    property.Value = value;
                }
                UpdateColorHex();
            }
        }
    }

    #endregion

    #region ColorHex

    private string _colorHex = "#000";
    public string ColorHex
    {
        get => _colorHex;
        private set => SetField(ref _colorHex, value);
    }

    #endregion
    
    public ColorPropertyViewModel() { }

    public ColorPropertyViewModel(SerializedProperty property) : base(property)
    {
        GetNestedProperty("r")!.ValueChangedEvent += HandleRValueChangedEvent;
        GetNestedProperty("g")!.ValueChangedEvent += HandleGValueChangedEvent;
        GetNestedProperty("b")!.ValueChangedEvent += HandleBValueChangedEvent;
        GetNestedProperty("a")!.ValueChangedEvent += HandleAValueChangedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        
        GetNestedProperty("r")!.ValueChangedEvent -= HandleRValueChangedEvent;
        GetNestedProperty("g")!.ValueChangedEvent -= HandleGValueChangedEvent;
        GetNestedProperty("b")!.ValueChangedEvent -= HandleBValueChangedEvent;
        GetNestedProperty("a")!.ValueChangedEvent -= HandleAValueChangedEvent;
    }

    private void HandleAValueChangedEvent(object? obj)
    {
        A = ConvertToFloat(obj, 1f);
    }

    private void HandleBValueChangedEvent(object? obj)
    {
        B = ConvertToFloat(obj, 0f);
    }

    private void HandleGValueChangedEvent(object? obj)
    {
        G = ConvertToFloat(obj, 0f);
    }

    private void HandleRValueChangedEvent(object? obj)
    {
        R = ConvertToFloat(obj, 0f);
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        R = ConvertToFloat(GetNestedProperty("r")?.Value, 0f);
        G = ConvertToFloat(GetNestedProperty("g")?.Value, 0f);
        B = ConvertToFloat(GetNestedProperty("b")?.Value, 0f);
        A = ConvertToFloat(GetNestedProperty("a")?.Value, 1f);
        UpdateColorHex();
    }

    private void UpdateColorHex()
    {
        var a = (int)(Clamp01(_a) * 255);
        var r = (int)(Clamp01(_r) * 255);
        var g = (int)(Clamp01(_g) * 255);
        var b = (int)(Clamp01(_b) * 255);
        var hex = ColorTranslator.ToHtml(Color.FromArgb(a, r, g, b));
        ColorHex = hex;
    }

    private static float ConvertToFloat(object? value, float defaultValue)
    {
        if (value is null) return defaultValue;
        if (value is JToken token) value = token.ToObject<object?>();
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal dec) return (float)dec;

        var text = value?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedInvariant))
            {
                return parsedInvariant;
            }

            if (float.TryParse(text, out var parsedCurrent))
            {
                return parsedCurrent;
            }
        }

        return defaultValue;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
