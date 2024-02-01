using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ReiEditor.Models.Services.Assets;

public class AssetRegistry : IAssetRegistry
{
    private readonly Dictionary<string, AssetInfo> _idToAssetInfoMap = new();
    private readonly Dictionary<AssetInfo, Asset> _loadedAssets = new();
    private readonly Dictionary<Asset, AssetInfo> _assetToAssetInfoMap = new();

    public bool Exists<T>(string assetId) where T : Asset => _idToAssetInfoMap.ContainsKey(assetId) && _idToAssetInfoMap[assetId].GetType() == typeof(T);

    public bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo) => _idToAssetInfoMap.TryGetValue(assetId, out assetInfo);
    
    public bool TryGetByPath(string fullPath, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo)
    {
        assetInfo = _idToAssetInfoMap.FirstOrDefault(x => x.Value.FullPath == fullPath).Value;
        return assetInfo != null;
    }

    public bool TryGetLoadedAsset(string assetId, [NotNullWhen(returnValue: true)] out Asset? asset)
    {
        asset = null;
        return TryGetById(assetId, out var assetInfo) && _loadedAssets.TryGetValue(assetInfo, out asset);
    }

    public IEnumerable<(AssetInfo, Asset)> GetDirtyAssets() => _loadedAssets.Select(keyValuePair => (keyValuePair.Key, keyValuePair.Value));

    public AssetInfo GetAssetInfo(Asset asset) => _assetToAssetInfoMap[asset];

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
        if (_loadedAssets.ContainsKey(assetInfo)) throw new Exception($"Asset already exists in {nameof(_loadedAssets)}");
		
        asset.SetAssetInfo(assetInfo);
        _loadedAssets.Add(assetInfo, asset);
        _assetToAssetInfoMap.Add(asset, assetInfo);
        _idToAssetInfoMap[assetInfo.Meta.AssetId] = assetInfo;
    }
}