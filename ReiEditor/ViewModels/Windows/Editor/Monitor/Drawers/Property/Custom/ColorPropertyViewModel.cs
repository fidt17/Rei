using System;
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
            }
        }
    }

    #endregion

    public ColorPropertyViewModel() { }

    public ColorPropertyViewModel(SerializedProperty property) : base(property) { }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        R = Convert.ToSingle(GetNestedProperty("r")?.Value ?? 0);
        G = Convert.ToSingle(GetNestedProperty("g")?.Value ?? 0);
        B = Convert.ToSingle(GetNestedProperty("b")?.Value ?? 0);
        A = Convert.ToSingle(GetNestedProperty("a")?.Value ?? 1);
    }
}