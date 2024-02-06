using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetRegistry
{
    bool TryGetById(string assetId, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo);
    bool TryGetByPath(string fullPath, [NotNullWhen(returnValue: true)] out AssetInfo? assetInfo);
    bool TryGetLoadedAsset(string assetId, [NotNullWhen(returnValue: true)] out Asset? asset);

    void RegisterAssets(IEnumerable<AssetInfo> assets);
    
    IEnumerable<Asset> GetDirtyAssets();
    IEnumerable<AssetInfo> GetAllAssets();
    
    void AddToLoadedAssets(AssetInfo assetInfo, Asset asset);
}