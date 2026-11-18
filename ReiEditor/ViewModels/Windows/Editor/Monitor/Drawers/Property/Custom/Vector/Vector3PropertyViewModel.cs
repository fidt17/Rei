using System;
using System.Numerics;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

public class Vector3PropertyViewModel : BaseCustomPropertyViewModel
{
    #region X

    private float _x;
    public float X
    {
        get => _x;
        set => SetCoordinate("x", value, new Vector3(value, ReadCoordinate("y"), ReadCoordinate("z")));
    }

    #endregion
    
    #region Y

    private float _y;
    public float Y
    {
        get => _y;
        set => SetCoordinate("y", value, new Vector3(ReadCoordinate("x"), value, ReadCoordinate("z")));
    }

    #endregion
    
    #region Z

    private float _z;
    public float Z
    {
        get => _z;
        set => SetCoordinate("z", value, new Vector3(ReadCoordinate("x"), ReadCoordinate("y"), value));
    }

    #endregion
    
    private readonly Action<object>? _commitProperty;

    public Vector3PropertyViewModel() { }

    public Vector3PropertyViewModel(SerializedProperty property, Action<object>? commitProperty = null) : base(property)
    {
        _commitProperty = commitProperty;
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        SetField(ref _x, ReadCoordinate("x"), nameof(X));
        SetField(ref _y, ReadCoordinate("y"), nameof(Y));
        SetField(ref _z, ReadCoordinate("z"), nameof(Z));
    }

    private float ReadCoordinate(string name) => Convert.ToSingle(GetNestedProperty(name)?.Value ?? 0);

    private void SetCoordinate(string name, float value, Vector3 vector)
    {
        if (!float.IsFinite(value) || GetNestedProperty(name) is not { } coordinate || Convert.ToSingle(coordinate.Value) == value) return;
        if (_commitProperty == null) coordinate.Value = value;
        else _commitProperty(vector);
    }
}
