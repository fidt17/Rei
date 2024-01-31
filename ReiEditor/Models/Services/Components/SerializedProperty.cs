using System;
using ReiEditor.Models.Services.Assets.Behaviours.Types;

namespace ReiEditor.Models.Services.Components;

public class SerializedProperty
{
    public string Name { get; }
    
    public ISerializedType Type { get; }

    public object? Value
    {
        get => _value;
        set
        {
            if (Type.IsValidValue(value))
            {
                _value = value;
            }
            else
            {
                throw new Exception($"Cannot assign value of type {value?.GetType()} to property {Type.GetType()}");
            }
        }
    }

    private object? _value;

    public SerializedProperty(string name, ISerializedType type, object value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}