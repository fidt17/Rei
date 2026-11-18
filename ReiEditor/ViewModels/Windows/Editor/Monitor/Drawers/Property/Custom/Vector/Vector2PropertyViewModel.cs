using System;
using System.Numerics;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

public class Vector2PropertyViewModel : BaseCustomPropertyViewModel
{
    #region X

    private float _x;
    public float X
    {
        get => _x;
        set => SetCoordinate("x", value, new Vector2(value, ReadCoordinate("y")));
    }

    #endregion

    #region Y

    private float _y;
    public float Y
    {
        get => _y;
        set => SetCoordinate("y", value, new Vector2(ReadCoordinate("x"), value));
    }

    #endregion

    private readonly Action<object>? _commitProperty;

    public Vector2PropertyViewModel() { }

    public Vector2PropertyViewModel(SerializedProperty property, Action<object>? commitProperty = null) : base(property)
    {
        _commitProperty = commitProperty;
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        SetField(ref _x, ReadCoordinate("x"), nameof(X));
        SetField(ref _y, ReadCoordinate("y"), nameof(Y));
    }

    private float ReadCoordinate(string name) => Convert.ToSingle(GetNestedProperty(name)?.Value ?? 0);

    private void SetCoordinate(string name, float value, Vector2 vector)
    {
        if (!float.IsFinite(value) || GetNestedProperty(name) is not { } coordinate || Convert.ToSingle(coordinate.Value) == value) return;
        if (_commitProperty == null) coordinate.Value = value;
        else _commitProperty(vector);
    }
}
