using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Components;

public class BehaviourComponent
{
    [JsonIgnore]
    public int Id => _id;
    
    [JsonProperty("Id")]
    private readonly int _id;

    private readonly List<SerializedProperty> _properties = new();

    [JsonProperty("SerializedData")]
    private readonly Dictionary<string, object?> _serializedData = new();

    public BehaviourComponent(int id)
    {
        _id = id;
    }

    public void AddProperty(SerializedProperty property)
    {
        if (_properties.Exists(x => x.Name == property.Name)) throw new Exception($"Another property with name {property.Name} already exists");
        _properties.Add(property);
        UpdateSerializedData(property);
    }

    public void SetPropertyValue(string propertyName, object? value)
    {
        var property = _properties.Find(x => x.Name == propertyName);
        if (property == null) throw new Exception($"Could not find property with name {propertyName}");

        property.Value = value;
        UpdateSerializedData(property);
    }

    private void UpdateSerializedData(SerializedProperty property)
    {
        _serializedData[property.Name] = property.Value;
    }
}