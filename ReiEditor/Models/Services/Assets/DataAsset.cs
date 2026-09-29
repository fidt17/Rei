using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Assets;

public sealed class DataAsset : Asset
{
    [JsonIgnore]
    public IReadOnlyDictionary<string, SerializedProperty> Properties => _properties;

    [JsonProperty("DataAssetTypeId")]
    public int DataAssetTypeId { get; private set; } = -1;

    [JsonProperty("SerializedData")]
    private readonly Dictionary<string, SerializedProperty> _properties = new();

    public DataAsset()
    {
    }

    public DataAsset(int dataAssetTypeId)
    {
        DataAssetTypeId = dataAssetTypeId;
    }

    public bool HasProperty(string name) => _properties.ContainsKey(name);

    public SerializedProperty GetProperty(string name) => _properties[name];

    public void AddProperty(SerializedProperty property)
    {
        if (HasProperty(property.Name))
        {
            throw new Exception($"Another DataAsset property with name {property.Name} already exists");
        }

        _properties.Add(property.Name, property);
    }

    public void RemoveProperty(string name)
    {
        _properties.Remove(name);
    }
}
