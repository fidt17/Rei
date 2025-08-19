using System;
using System.Drawing;
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
        A = Convert.ToSingle(obj);
    }

    private void HandleBValueChangedEvent(object? obj)
    {
        B = Convert.ToSingle(obj);
    }

    private void HandleGValueChangedEvent(object? obj)
    {
        G = Convert.ToSingle(obj);
    }

    private void HandleRValueChangedEvent(object? obj)
    {
        R = Convert.ToSingle(obj);
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        R = Convert.ToSingle(GetNestedProperty("r")?.Value ?? 0);
        G = Convert.ToSingle(GetNestedProperty("g")?.Value ?? 0);
        B = Convert.ToSingle(GetNestedProperty("b")?.Value ?? 0);
        A = Convert.ToSingle(GetNestedProperty("a")?.Value ?? 1);
        UpdateColorHex();
    }

    private void UpdateColorHex()
    {
        var hex = ColorTranslator.ToHtml(Color.FromArgb((int) (_a * 255), (int) (_r * 255), (int) (_g * 255), (int) (_b * 255)));
        ColorHex = hex;
    }
}