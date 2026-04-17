using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Models.Services.Components;

[JsonConverter(typeof(SerializedPropertyJsonConverter))]
public class SerializedProperty
{
    public event Action<object?>? ValueChangedEvent;
    
    public string Name { get; private set; }
    public SerializedTypeEnum Type { get; }
    public string SourceType { get; }
    [JsonIgnore] public string? TemplateTypeName { get; private set; }
    [JsonIgnore] public SerializedTypeEnum ItemType { get; }
    [JsonIgnore] public string? ItemSourceType { get; }
    [JsonIgnore] public string? ItemTemplateTypeName { get; }
    [JsonIgnore] public SerializedProperty? ParentProperty { get; }

    [JsonIgnore]
    public object? Value
    {
        get => _value;
        set => SetValueInternal(value, triggerChangedEvent: true);
    }

    [JsonProperty("Value")]
    private object? _value;

    [JsonIgnore]
    private readonly List<SerializedProperty> _trackedChildren = new();

    public SerializedProperty(
        string name,
        SerializedTypeEnum type,
        object? value,
        string sourceType,
        SerializedProperty? parentProperty,
        string? templateTypeName = null,
        SerializedTypeEnum itemType = SerializedTypeEnum.Invalid,
        string? itemSourceType = null,
        string? itemTemplateTypeName = null)
    {
        Name = name;
        Type = type;
        SourceType = sourceType;
        ParentProperty = parentProperty;
        TemplateTypeName = templateTypeName;
        ItemType = itemType;
        ItemSourceType = itemSourceType;
        ItemTemplateTypeName = itemTemplateTypeName;
        Value = value;
    }

    public void SetTemplateTypeName(string? templateTypeName)
    {
        TemplateTypeName = templateTypeName;
    }

    public void SetName(string name)
    {
        Name = name;
    }

    public void FillPropertyHierarchy(List<SerializedProperty> hierarchy)
    {
        hierarchy.Insert(0, this);
        ParentProperty?.FillPropertyHierarchy(hierarchy);
    }

    public void SetValueWithoutTriggeringChangedEvent(object value)
        => SetValueInternal(value, triggerChangedEvent: false);

    public void TriggerChangedEvent()
    {
        ValueChangedEvent?.Invoke(_value);
    }

    public void NotifyStructureChanged()
    {
        RefreshChildSubscriptions(_value, unsubscribeOnly: false);
        TriggerChangedEvent();
    }

    private void SetValueInternal(object? value, bool triggerChangedEvent)
    {
        try
        {
            if (_value == value || (_value != null && _value.Equals(value))) return;

            if (value == null && Type != SerializedTypeEnum.Custom && Type != SerializedTypeEnum.Collection) return;

            if (Type.IsValidValue(value))
            {
                if (value is Dictionary<string, object?> valueDict)
                {
                    var nestedProperties = Value as Dictionary<string, SerializedProperty>;
                    foreach (var (k, v) in valueDict)
                    {
                        if (nestedProperties!.TryGetValue(k, out var property))
                        {
                            property.Value = v;
                        }
                    }
                }
                else if (value is List<object?> valueList && _value is List<SerializedProperty> nestedProperties)
                {
                    var count = Math.Min(valueList.Count, nestedProperties.Count);
                    for (var index = 0; index < count; index++)
                    {
                        nestedProperties[index].Value = valueList[index];
                    }
                }
                else
                {
                    RefreshChildSubscriptions(_value, unsubscribeOnly: true);
                    _value = value;
                    RefreshChildSubscriptions(_value, unsubscribeOnly: false);
                    if (triggerChangedEvent)
                    {
                        ValueChangedEvent?.Invoke(_value);
                    }
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

    private void RefreshChildSubscriptions(object? value, bool unsubscribeOnly)
    {
        foreach (var child in _trackedChildren)
        {
            child.ValueChangedEvent -= HandleChildValueChangedEvent;
        }
        _trackedChildren.Clear();

        if (unsubscribeOnly) return;

        foreach (var child in GetChildren(value))
        {
            child.ValueChangedEvent += HandleChildValueChangedEvent;
            _trackedChildren.Add(child);
        }
    }

    private static IEnumerable<SerializedProperty> GetChildren(object? value)
    {
        if (value is Dictionary<string, SerializedProperty> valueDict)
        {
            return valueDict.Values;
        }

        if (value is List<SerializedProperty> valueList)
        {
            return valueList;
        }

        return Enumerable.Empty<SerializedProperty>();
    }

    private void HandleChildValueChangedEvent(object? _)
    {
        ValueChangedEvent?.Invoke(_value);
    }
}
