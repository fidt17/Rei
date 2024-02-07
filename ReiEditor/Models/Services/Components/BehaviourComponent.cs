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

    [JsonProperty("SerializedData")]
    private readonly Dictionary<string, SerializedProperty> _properties = new();

    public BehaviourComponent(int id)
    {
        _id = id;
    }

    public bool HasProperty(string name) => _properties.ContainsKey(name);

    public void AddProperty(SerializedProperty property)
    {
        if (HasProperty(property.Name)) throw new Exception($"Another property with name {property.Name} already exists");
        _properties.Add(property.Name, property);
    }

    public void SetPropertyValue(string propertyName, object? value)
    {
        if (!HasProperty(propertyName)) throw new Exception($"Could not find property with name {propertyName}");

        _properties[propertyName].Value = value;
    }

    public SerializedProperty GetProperty(string name) => _properties[name];
}