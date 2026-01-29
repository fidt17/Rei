using System.Collections.Generic;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public class AssetBuildCacheManifest
{
    public class AssetBuildCacheEntry
    {
        public string AssetId { get; set; } = "";
        public string AssetPath { get; set; } = "";
        public string ContentHash { get; set; } = "";
        public string CacheFileName { get; set; } = "";
        public long CacheSize { get; set; }
    }

    public string CacheKey { get; set; } = "";
    public Dictionary<string, AssetBuildCacheEntry> Entries { get; set; } = new();
}
