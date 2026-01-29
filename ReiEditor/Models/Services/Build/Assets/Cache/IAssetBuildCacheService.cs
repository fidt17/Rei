using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public interface IAssetBuildCacheService
{
    string GetCacheDirectory(string buildFolder);
    string GetManifestPath(string buildFolder);
    AssetBuildCacheManifest LoadOrCreateManifest(string buildFolder);
    void SaveManifest(string buildFolder, AssetBuildCacheManifest manifest);
    string ComputeContentHash(string assetPath);
    string GetCacheFileName(AssetInfo assetInfo, string contentHash);
    string GetCacheFilePath(string buildFolder, string cacheFileName);
    bool TryGetCacheEntry(string buildFolder, AssetBuildCacheManifest manifest, AssetInfo assetInfo, string contentHash, out AssetBuildCacheManifest.AssetBuildCacheEntry entry, out string cacheFilePath);
    AssetBuildCacheManifest.AssetBuildCacheEntry CreateEntry(AssetInfo assetInfo, string contentHash, string cacheFileName, long cacheSize);
    void AddEntry(AssetBuildCacheManifest manifest, AssetBuildCacheManifest.AssetBuildCacheEntry entry);
    void PruneUnusedCacheFiles(string buildFolder, AssetBuildCacheManifest manifest);
}
