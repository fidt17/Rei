using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.Services.Assets;

public class AssetRegistry : IAssetRegistry
{
    private readonly Dictionary<string, AssetInfo> _idToAssetInfoMap = new();
    private readonly Dictionary<string, Asset> _loadedAssets = new();
    
    private readonly ILogger<AssetRegistry> _logger;

    public AssetRegistry(ILogger<AssetRegistry> logger)
    {
        _logger = logger;
    }

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

    public IEnumerable<AssetInfo> GetAllAssetsByExtensions(IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0) yield break;

        foreach (var asset in _idToAssetInfoMap.Values)
        {
            var extension = Path.GetExtension(asset.FullPath);
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;

            yield return asset;
        }
    }

    public void UpdateRegistry(IEnumerable<AssetInfo> assets)
    {
        _idToAssetInfoMap.Clear();
        foreach (var asset in assets)
        {
            _idToAssetInfoMap[asset.Meta.AssetId] = asset;
        }
    }
    
    public void RegisterNewAssets(IEnumerable<AssetInfo> assets)
    {
        foreach (var asset in assets)
        {
            if (!_idToAssetInfoMap.TryAdd(asset.Meta.AssetId, asset))
            {
                _logger.LogWarning($"Asset {asset.Meta.AssetId} {asset.FullPath} already registered");
            }
        }
    }

    public void UpdateRegistryPath(string oldPath, string newPath)
    {
        var isDirectory = Directory.Exists(newPath);
        
        var oldFullPath = oldPath.ToFullPath();
        var newFullPath = newPath.ToFullPath();

        var assets = _idToAssetInfoMap.Values.ToList();
        var updated = new List<AssetInfo>();
        var changed = false;

        foreach (var asset in assets)
        {
            var assetFullPath = asset.FullPath.ToFullPath();
            if (isDirectory)
            {
                if (!assetFullPath.IsUnderDirectory(oldFullPath))
                {
                    updated.Add(asset);
                    continue;
                }

                var relative = Path.GetRelativePath(oldFullPath, assetFullPath);
                var updatedPath = Path.Combine(newFullPath, relative);
                updated.Add(new AssetInfo(asset.Meta, updatedPath));
                changed = true;
                continue;
            }

            if (!assetFullPath.PathEquals(oldFullPath))
            {
                updated.Add(asset);
                continue;
            }

            updated.Add(new AssetInfo(asset.Meta, newFullPath));
            changed = true;
        }

        if (changed)
        {
            UpdateRegistry(updated);
        }
    }

    public void UnregisterByPath(string fullPath)
    {
        UpdateRegistryWithFilter(asset => !asset.FullPath.PathEquals(fullPath));
    }

    public void UnregisterUnderDirectory(string directoryPath)
    {
        UpdateRegistryWithFilter(asset => !asset.FullPath.IsUnderDirectory(directoryPath));
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

    private void UpdateRegistryWithFilter(Func<AssetInfo, bool> keepPredicate)
    {
        var assets = _idToAssetInfoMap.Values.ToList();
        var updated = assets.Where(keepPredicate).ToList();
        if (updated.Count == assets.Count) return;
        UpdateRegistry(updated);
    }
}
