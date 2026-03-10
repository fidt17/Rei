using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public interface IAssetBuildCacheService
{
    string GetManifestPath(string cacheDirectory);
    AssetBuildCacheManifest LoadOrCreateManifest(string cacheDirectory);
    void SaveManifest(string cacheDirectory, AssetBuildCacheManifest manifest);
    string ComputeContentHash(string assetPath);
    string GetCacheFileName(AssetInfo assetInfo, string contentHash);
    string GetCacheFilePath(string cacheDirectory, string cacheFileName);
    bool TryGetCacheEntry(string cacheDirectory, AssetBuildCacheManifest manifest, AssetInfo assetInfo, string contentHash, out AssetBuildCacheManifest.AssetBuildCacheEntry entry, out string cacheFilePath);
    AssetBuildCacheManifest.AssetBuildCacheEntry CreateEntry(AssetInfo assetInfo, string contentHash, string cacheFileName, long cacheSize);
    void AddEntry(AssetBuildCacheManifest manifest, AssetBuildCacheManifest.AssetBuildCacheEntry entry);
    void PruneUnusedCacheFiles(string cacheDirectory, AssetBuildCacheManifest manifest);
}
