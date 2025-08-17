using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ReiEditor.Models.Services.Assets;

public class AssetRegistry : IAssetRegistry
{
    private readonly Dictionary<string, AssetInfo> _idToAssetInfoMap = new();
    private readonly Dictionary<string, Asset> _loadedAssets = new();

    public bool Exists<T>(string assetId) where T : Asset => _idToAssetInfoMap.ContainsKey(assetId) && _idToAssetInfoMap[assetId].GetType() == typeof(T);

    public bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo) => _idToAssetInfoMap.TryGetValue(assetId, out assetInfo);
    
    public bool TryGetByPath(string fullPath, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo)
    {
        assetInfo = _idToAssetInfoMap.FirstOrDefault(x => x.Value.FullPath == fullPath).Value;
        return assetInfo != null;
    }

    public bool TryGetLoadedAsset(string assetId, [NotNullWhen(returnValue: true)] out Asset? asset) => _loadedAssets.TryGetValue(assetId, out asset);

    public IEnumerable<Asset> GetDirtyAssets()
    {
        foreach (var keyValuePair in _loadedAssets)
        {
            var asset = keyValuePair.Value;
            yield return asset;
        }
    }

    public IEnumerable<AssetInfo> GetAllAssets() => _idToAssetInfoMap.Values;

    public void RegisterAssets(IEnumerable<AssetInfo> assets)
    {
        _idToAssetInfoMap.Clear();
        foreach (var asset in assets)
        {
            _idToAssetInfoMap[asset.Meta.AssetId] = asset;
        }
    }

    public void AddToLoadedAssets(AssetInfo assetInfo, Asset asset)
    {
        if (_loadedAssets.ContainsKey(assetInfo.Meta.AssetId)) throw new Exception($"Asset {assetInfo.Meta.AssetId} {assetInfo.FullPath} is already loaded");
        
        asset.SetAssetInfo(assetInfo);
        _loadedAssets[assetInfo.Meta.AssetId] = asset;
        _idToAssetInfoMap[assetInfo.Meta.AssetId] = assetInfo;
    }

    public void RemoveFromLoadedAssets(AssetInfo assetInfo)
    {
        _loadedAssets.Remove(assetInfo.Meta.AssetId);
    }
}