using System;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Behaviours.Types;

namespace ReiEditor.Models.Services.Components;

public class SerializedProperty
{
    public event Action<object?>? ValueChangedEvent;
    
    public string Name { get; }
    public SerializedTypeEnum Type { get; }

    [JsonIgnore]
    public object? Value
    {
        get => _value;
        set
        {
            if (Type.IsValidValue(value))
            {
                _value = value;
                ValueChangedEvent?.Invoke(_value);
            }
            else
            {
                throw new Exception($"Cannot assign value of type {value?.GetType()} to property {Type.GetType()}");
            }
        }
    }

    [JsonProperty("Value")]
    private object? _value;

    public SerializedProperty(string name, SerializedTypeEnum type, object value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}