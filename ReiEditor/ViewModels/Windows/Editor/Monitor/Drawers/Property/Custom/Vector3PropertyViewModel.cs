using System;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class Vector3PropertyViewModel : BaseCustomPropertyViewModel
{
    #region X

    private float _x;
    public float X
    {
        get => _x;
        set
        {
            if (SetField(ref _x, value))
            {
                var property = GetNestedProperty("x");
                if (property != null)
                {
                    property.Value = value;
                }
            }
        }
    }

    #endregion
    
    #region Y

    private float _y;
    public float Y
    {
        get => _y;
        set
        {
            if (SetField(ref _y, value))
            {
                var property = GetNestedProperty("y");
                if (property != null)
                {
                    property.Value = value;
                }
            }
        }
    }

    #endregion
    
    #region Z

    private float _z;
    public float Z
    {
        get => _z;
        set
        {
            if (SetField(ref _z, value))
            {
                var property = GetNestedProperty("z");
                if (property != null)
                {
                    property.Value = value;
                }
            }
        }
    }

    #endregion
    
    public Vector3PropertyViewModel() { }

    public Vector3PropertyViewModel(SerializedProperty property) : base(property) { }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        X = Convert.ToSingle(GetNestedProperty("x")?.Value ?? 0);
        Y = Convert.ToSingle(GetNestedProperty("y")?.Value ?? 0);
        Z = Convert.ToSingle(GetNestedProperty("z")?.Value ?? 0);
    }
}