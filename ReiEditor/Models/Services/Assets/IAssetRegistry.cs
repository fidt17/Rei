using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetRegistry
{
    bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo);
    bool TryGetByPath(string fullPath, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo);
    bool TryGetLoadedAsset(string assetId, [NotNullWhen(returnValue: true)] out Asset? asset);

    void UpdateRegistry(IEnumerable<AssetInfo> assets);
    void RegisterNewAssets(IEnumerable<AssetInfo> assets);

    void UpdateRegistryPath(string oldPath, string newPath);
    void UnregisterByPath(string fullPath);
    void UnregisterUnderDirectory(string directoryPath);
    
    IEnumerable<Asset> GetDirtyAssets();
    IEnumerable<AssetInfo> GetAllAssets();
    
    void AddToLoadedAssets(AssetInfo assetInfo, Asset asset);
    void RemoveFromLoadedAssets(AssetInfo assetInfo);
}
