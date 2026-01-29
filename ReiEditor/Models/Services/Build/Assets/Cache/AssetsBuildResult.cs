using System.Collections.Generic;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public class AssetsBuildResult
{
    public BuildAssetMap Map { get; }
    public AssetsBuildCacheReport Report { get; }

    public AssetsBuildResult(BuildAssetMap map, AssetsBuildCacheReport report)
    {
        Map = map;
        Report = report;
    }
}

public class AssetsBuildCacheReport
{
    public int TotalAssets { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public long TotalBytes { get; set; }
    public long TotalBuildMs { get; set; }
    public List<AssetsBuildEntryReport> BuiltAssets { get; } = new();
}

public class AssetsBuildEntryReport
{
    public string AssetId { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public long BuildMs { get; set; }
    public long SizeBytes { get; set; }
}