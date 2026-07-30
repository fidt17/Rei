using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

internal sealed class Vector2PropertyBinding
{
    public event Action? Changed;

    private readonly SerializedProperty _property;
    private readonly SerializedProperty _x;
    private readonly SerializedProperty _y;

    public Vector2PropertyBinding(SerializedProperty property)
    {
        _property = property;
        _x = GetNestedProperty(property, "x");
        _y = GetNestedProperty(property, "y");

        _property.ValueChangedEvent += HandleValueChanged;
    }

    public float X
    {
        get => Convert.ToSingle(_x.Value ?? 0f);
        set => _x.Value = value;
    }

    public float Y
    {
        get => Convert.ToSingle(_y.Value ?? 0f);
        set => _y.Value = value;
    }

    public void SetSilently(float x, float y)
    {
        _x.SetValueWithoutTriggeringChangedEvent(x);
        _y.SetValueWithoutTriggeringChangedEvent(y);
    }

    public void Dispose()
    {
        _property.ValueChangedEvent -= HandleValueChanged;
    }

    private static SerializedProperty GetNestedProperty(SerializedProperty property, string name)
    {
        if (property.Value is not IReadOnlyDictionary<string, SerializedProperty> nestedProperties)
        {
            throw new Exception($"Property {property.Name} is not Vector2-like");
        }

        return nestedProperties.TryGetValue(name, out var nestedProperty)
            ? nestedProperty
            : throw new Exception($"Property {property.Name} does not have {name}");
    }

    private void HandleValueChanged(object? _) => Changed?.Invoke();
}
