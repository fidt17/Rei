using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

internal sealed class Vector2PropertyBinding
{
    public event Action? Changed;

    private readonly SerializedProperty _property;

    public Vector2PropertyBinding(SerializedProperty property)
    {
        _property = property;
        GetNestedProperty(property, "x");
        GetNestedProperty(property, "y");

        _property.ValueChangedEvent += HandleValueChanged;
    }

    public float X
    {
        get => Convert.ToSingle(GetNestedProperty(_property, "x").Value ?? 0f);
        set => GetNestedProperty(_property, "x").Value = value;
    }

    public float Y
    {
        get => Convert.ToSingle(GetNestedProperty(_property, "y").Value ?? 0f);
        set => GetNestedProperty(_property, "y").Value = value;
    }

    public void SetSilently(float x, float y)
    {
        GetNestedProperty(_property, "x").SetValueWithoutTriggeringChangedEvent(x);
        GetNestedProperty(_property, "y").SetValueWithoutTriggeringChangedEvent(y);
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
