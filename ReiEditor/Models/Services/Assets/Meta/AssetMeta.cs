using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReiEditor.Models.Services.Assets.Meta;

public class AssetMeta
{
    [JsonProperty("AssetId")]
    public string AssetId { get; }

    [JsonProperty("Data")]
    private Dictionary<string, object?> _data { get; } = new();

    public AssetMeta(string assetId)
    {
        AssetId = assetId;
    }

    public AssetMeta CreateCopyWithId(string newId)
    {
        var meta = new AssetMeta(newId);
        foreach (var entry in _data)
        {
            meta._data[entry.Key] = entry.Value;
        }
        return meta;
    }

    public void AddData<T>(string key, T data)
    {
        _data[key] = data;
    }

    public T? GetData<T>(string key)
    {
        if (_data.ContainsKey(key) && _data[key] is JObject)
        {
            var desObj = ((JObject)_data[key]!).ToObject<T>();
            _data[key] = desObj;
            return desObj;
        }
        
        if (_data.ContainsKey(key))
        {
            return (T?) _data[key];
        }

        return default;
    }

    public bool TryGetData<T>(string key, [NotNullWhen(returnValue: true)] out T? value)
    {
        value = GetData<T>(key);
        return _data.ContainsKey(key);
    }
}
