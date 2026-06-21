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
    private readonly object _lock = new();
    
    private readonly ILogger<AssetRegistry> _logger;

    public AssetRegistry(ILogger<AssetRegistry> logger)
    {
        _logger = logger;
    }

    public bool Exists<T>(string assetId) where T : Asset
    {
        lock (_lock)
        {
            return _idToAssetInfoMap.ContainsKey(assetId) && _idToAssetInfoMap[assetId].GetType() == typeof(T);
        }
    }

    public bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo)
    {
        lock (_lock)
        {
            return _idToAssetInfoMap.TryGetValue(assetId, out assetInfo);
        }
    }

    public bool TryGetByIdAndExtensions(string assetId, IReadOnlyCollection<string> extensions, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo)
    {
        assetInfo = null;

        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (extensions.Count == 0) return false;
        AssetInfo? resolvedAssetInfo;
        lock (_lock)
        {
            if (!_idToAssetInfoMap.TryGetValue(assetId, out resolvedAssetInfo)) return false;
        }

        var extension = Path.GetExtension(resolvedAssetInfo.FullPath);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;

        assetInfo = resolvedAssetInfo;
        return true;
    }
    
    public bool TryGetByPath(string fullPath, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo)
    {
        lock (_lock)
        {
            assetInfo = _idToAssetInfoMap.FirstOrDefault(x => x.Value.FullPath == fullPath).Value;
        }
        return assetInfo != null;
    }

    public bool TryGetLoadedAsset(string assetId, [NotNullWhen(returnValue: true)] out Asset? asset)
    {
        lock (_lock)
        {
            return _loadedAssets.TryGetValue(assetId, out asset);
        }
    }

    public IEnumerable<Asset> GetDirtyAssets()
    {
        List<Asset> loadedAssets;
        lock (_lock)
        {
            loadedAssets = _loadedAssets.Values.ToList();
        }

        foreach (var asset in loadedAssets)
        {
            yield return asset;
        }
    }

    public IEnumerable<AssetInfo> GetLoadedAssetInfos()
    {
        List<string> loadedAssetIds;
        lock (_lock)
        {
            loadedAssetIds = _loadedAssets.Keys.ToList();
        }

        foreach (var assetId in loadedAssetIds)
        {
            AssetInfo? assetInfo;
            lock (_lock)
            {
                if (!_idToAssetInfoMap.TryGetValue(assetId, out assetInfo)) continue;
            }

            yield return assetInfo;
        }
    }

    public IEnumerable<AssetInfo> GetAllAssets()
    {
        lock (_lock)
        {
            return _idToAssetInfoMap.Values.ToList();
        }
    }

    public IEnumerable<AssetInfo> GetAllAssetsByExtensions(IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0) yield break;

        List<AssetInfo> allAssets;
        lock (_lock)
        {
            allAssets = _idToAssetInfoMap.Values.ToList();
        }

        foreach (var asset in allAssets)
        {
            var extension = Path.GetExtension(asset.FullPath);
            if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;

            yield return asset;
        }
    }

    public bool IsUniqueAssetName(string assetName, string assetExtension)
    {
        return !GetAllAssetsByExtensions(new [] { assetExtension} )
            .Any(x => string.Equals(Path.GetFileNameWithoutExtension(x.FullPath), assetName, StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateRegistry(IEnumerable<AssetInfo> assets)
    {
        lock (_lock)
        {
            _idToAssetInfoMap.Clear();
            foreach (var asset in assets)
            {
                _idToAssetInfoMap[asset.Meta.AssetId] = asset;
            }

            PruneLoadedAssetsWithoutRegistryEntries();
        }
    }
    
    public void RegisterNewAssets(IEnumerable<AssetInfo> assets)
    {
        lock (_lock)
        {
            foreach (var asset in assets)
            {
                if (!_idToAssetInfoMap.TryAdd(asset.Meta.AssetId, asset))
                {
                    _logger.LogWarning($"Asset {asset.Meta.AssetId} {asset.FullPath} already registered");
                }
            }
        }
    }

    public void UpdateRegistryPath(string oldPath, string newPath)
    {
        var isDirectory = Directory.Exists(newPath);
        
        var oldFullPath = oldPath.ToFullPath();
        var newFullPath = newPath.ToFullPath();

        List<AssetInfo> assets;
        lock (_lock)
        {
            assets = _idToAssetInfoMap.Values.ToList();
        }
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
        lock (_lock)
        {
            if (_loadedAssets.ContainsKey(assetInfo.Meta.AssetId)) throw new Exception($"Asset {assetInfo.Meta.AssetId} {assetInfo.FullPath} is already loaded");
        
            asset.SetAssetInfo(assetInfo);
            _loadedAssets[assetInfo.Meta.AssetId] = asset;
            _idToAssetInfoMap[assetInfo.Meta.AssetId] = assetInfo;
        }
    }

    public void RemoveFromLoadedAssets(AssetInfo assetInfo)
    {
        lock (_lock)
        {
            _loadedAssets.Remove(assetInfo.Meta.AssetId);
        }
    }

    private void UpdateRegistryWithFilter(Func<AssetInfo, bool> keepPredicate)
    {
        List<AssetInfo> assets;
        lock (_lock)
        {
            assets = _idToAssetInfoMap.Values.ToList();
        }

        var updated = assets.Where(keepPredicate).ToList();
        if (updated.Count == assets.Count) return;
        UpdateRegistry(updated);
    }

    private void PruneLoadedAssetsWithoutRegistryEntries()
    {
        if (_loadedAssets.Count == 0) return;

        var loadedAssetIds = _loadedAssets.Keys.ToList();
        foreach (var assetId in loadedAssetIds)
        {
            if (_idToAssetInfoMap.ContainsKey(assetId)) continue;

            _loadedAssets.Remove(assetId);
        }
    }
}
