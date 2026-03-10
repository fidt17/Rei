using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public class AssetBuildCachePipeline : IAssetBuildCachePipeline
{
    private readonly ILogger<AssetBuildCachePipeline> _logger;
    private readonly IAssetBuildCacheService _cacheService;

    public AssetBuildCachePipeline(ILogger<AssetBuildCachePipeline> logger, IAssetBuildCacheService cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
    }

    public AssetsBuildResult BuildAssets(
        IEngineApi engineApi,
        IEnumerable<AssetInfo> assetInfos,
        string buildFolder,
        string assetsBinPath,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        
        var manifest = _cacheService.LoadOrCreateManifest(buildFolder);
        var report = new AssetsBuildCacheReport();
        
        var map = BuildInternal(engineApi, assetInfos, buildFolder, assetsBinPath, manifest, report, forceRebuild, onAssetBuilding);
        _cacheService.SaveManifest(buildFolder, manifest);
        _cacheService.PruneUnusedCacheFiles(buildFolder, manifest);
        
        totalStopwatch.Stop();
        report.TotalBuildMs = totalStopwatch.ElapsedMilliseconds;
        
        return new AssetsBuildResult(map, report);
    }

    private BuildAssetMap BuildInternal(
        IEngineApi engineApi,
        IEnumerable<AssetInfo> assetInfos,
        string buildFolder,
        string assetsBinPath,
        AssetBuildCacheManifest manifest,
        AssetsBuildCacheReport report,
        bool forceRebuild,
        Action<AssetBuildProgressInfo>? onAssetBuilding)
    {
        const string INNER_PATH = "assets.bin";
        
        var map = new BuildAssetMap();
        var assetList = assetInfos.ToList();
        var totalAssets = assetList.Count;

        var total = 0;
        var cacheHits = 0;
        var cacheMisses = 0;
        long totalBytes = 0L;
        
        long offset = 0L;
        for (var i = 0; i < totalAssets; i++)
        {
            var assetInfo = assetList[i];
            onAssetBuilding?.Invoke(new AssetBuildProgressInfo(i + 1, totalAssets, assetInfo.FullPath));
            total++;
            var contentHash = _cacheService.ComputeContentHash(assetInfo.FullPath);
            if (!forceRebuild && _cacheService.TryGetCacheEntry(buildFolder, manifest, assetInfo, contentHash, out _, out var cacheFilePath))
            {
                cacheHits++;
                var bytesWritten = AppendCacheToAssets(assetsBinPath, cacheFilePath);
                totalBytes += bytesWritten;
                map.Add(new BuildAssetMap.AssetBuildInfo(assetInfo.Meta.AssetId, Path.GetFileName(assetInfo.FullPath), assetInfo.FullPath, INNER_PATH, offset));
                offset += bytesWritten;
                continue;
            }
            
            cacheMisses++;

            var cacheFileName = _cacheService.GetCacheFileName(assetInfo, contentHash);
            var cacheFile = _cacheService.GetCacheFilePath(buildFolder, cacheFileName);
            
            var buildStopwatch = Stopwatch.StartNew();
            var cacheBytes = BuildAssetToCache(engineApi, assetInfo.FullPath, cacheFile);
            buildStopwatch.Stop();
            
            if (cacheBytes > 0)
            {
                var entry = _cacheService.CreateEntry(assetInfo, contentHash, cacheFileName, cacheBytes);
                _cacheService.AddEntry(manifest, entry);
                AppendCacheToAssets(assetsBinPath, cacheFile);
                
                totalBytes += cacheBytes;
                report.BuiltAssets.Add(new AssetsBuildEntryReport
                {
                    AssetId = assetInfo.Meta.AssetId,
                    AssetPath = assetInfo.FullPath,
                    BuildMs = buildStopwatch.ElapsedMilliseconds,
                    SizeBytes = cacheBytes
                });
            }

            map.Add(new BuildAssetMap.AssetBuildInfo(assetInfo.Meta.AssetId, Path.GetFileName(assetInfo.FullPath), assetInfo.FullPath, INNER_PATH, offset));
            offset += cacheBytes;
        }

        report.TotalAssets = total;
        report.CacheHits = cacheHits;
        report.CacheMisses = cacheMisses;
        report.TotalBytes = totalBytes;
        _logger.Log($"Asset cache summary: total={total}, hits={cacheHits}, misses={cacheMisses}");

        return map;
    }

    private long BuildAssetToCache(IEngineApi engineApi, string assetPath, string cacheFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);

        engineApi.BuildAsset(assetPath, cacheFilePath, 0);

        var fileInfo = new FileInfo(cacheFilePath);
        var bytesWritten = fileInfo.Length;
        if (bytesWritten <= 0)
        {
            _logger.LogWarning($"Asset build produced no data: {assetPath}");
            if (File.Exists(cacheFilePath))
            {
                File.Delete(cacheFilePath);
            }
        }

        return bytesWritten;
    }

    private long AppendCacheToAssets(string assetsBinPath, string cacheFilePath)
    {
        using var cacheStream = new FileStream(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var assetsStream = new FileStream(assetsBinPath, FileMode.Append, FileAccess.Write, FileShare.Read);

        cacheStream.CopyTo(assetsStream);
        return cacheStream.Length;
    }

}
