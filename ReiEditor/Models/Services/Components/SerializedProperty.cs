using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Models.Services.Components;

public class SerializedProperty
{
    public event Action<object?>? ValueChangedEvent;
    
    public string Name { get; }
    public SerializedTypeEnum Type { get; }
    public string SourceType { get; }
    [JsonIgnore] public SerializedProperty? ParentProperty { get; }

    [JsonIgnore]
    public object? Value
    {
        get => _value;
        set
        {
            try
            {
                if (_value == value || (_value != null && _value.Equals(value))) return;
                
                if (Type.IsValidValue(value))
                {
                    if (value is Dictionary<string, object?> valueDict)
                    {
                        var nestedProperties = Value as Dictionary<string, SerializedProperty>;
                        foreach (var (k, v) in valueDict)
                        {
                            if (nestedProperties.TryGetValue(k, out var property))
                            {
                                property.Value = v;
                            }
                        }
                    }
                    else
                    {
                        _value = value;
                        ValueChangedEvent?.Invoke(_value);
                    }
                }
                else
                {
                    throw new Exception($"Cannot assign value of type {value?.GetType()} to property {Type}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    [JsonProperty("Value")]
    private object? _value;

    public SerializedProperty(string name, SerializedTypeEnum type, object? value, string sourceType, SerializedProperty? parentProperty)
    {
        Name = name;
        Type = type;
        Value = value;
        SourceType = sourceType;
        ParentProperty = parentProperty;
    }

    public void FillPropertyHierarchy(List<SerializedProperty> hierarchy)
    {
        hierarchy.Insert(0, this);
        ParentProperty?.FillPropertyHierarchy(hierarchy);
    }
}