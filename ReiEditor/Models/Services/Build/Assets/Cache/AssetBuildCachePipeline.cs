using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public class AssetBuildCachePipeline : IAssetBuildCachePipeline
{
    private sealed record PreparedAsset(AssetInfo Asset, string CacheFilePath, long SizeBytes, string ContentHash, bool CacheHit, long BuildMs = 0);

    private const int MODEL_WORKER_COUNT = 4;

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
        string cacheDirectory,
        string assetsBinPath,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        
        var manifest = _cacheService.LoadOrCreateManifest(cacheDirectory);
        var report = new AssetsBuildCacheReport();
        
        var preparedAssets = PrepareAssets(engineApi, assetInfos.ToList(), cacheDirectory, manifest, report, forceRebuild, onAssetBuilding);
        var map = PackAssets(preparedAssets, assetsBinPath, report);
        _logger.Log($"Asset cache summary: total={report.TotalAssets}, hits={report.CacheHits}, misses={report.CacheMisses}");
        _cacheService.SaveManifest(cacheDirectory, manifest);
        _cacheService.PruneUnusedCacheFiles(cacheDirectory, manifest);
        
        totalStopwatch.Stop();
        report.TotalBuildMs = totalStopwatch.ElapsedMilliseconds;
        
        return new AssetsBuildResult(map, report);
    }

    private List<PreparedAsset> PrepareAssets(IEngineApi engineApi, IReadOnlyList<AssetInfo> assets, string cacheDirectory,
        AssetBuildCacheManifest manifest, AssetsBuildCacheReport report, bool forceRebuild, Action<AssetBuildProgressInfo>? onAssetBuilding)
    {
        var preparedAssets = new List<PreparedAsset>(assets.Count);
        var modelsToConvert = new List<int>();
        var completed = 0;
        report.TotalAssets = assets.Count;
        for (var i = 0; i < assets.Count; i++)
        {
            var asset = assets[i];
            var contentHash = _cacheService.ComputeContentHash(asset.FullPath);
            if (!forceRebuild && _cacheService.TryGetCacheEntry(cacheDirectory, manifest, asset, contentHash, out var cachedEntry, out var cachedPath))
            {
                report.CacheHits++;
                preparedAssets.Add(new PreparedAsset(asset, cachedPath, cachedEntry.CacheSize, contentHash, true));
                ReportCompleted(asset);
                continue;
            }

            report.CacheMisses++;
            var cacheFileName = _cacheService.GetCacheFileName(asset, contentHash);
            var cacheFilePath = _cacheService.GetCacheFilePath(cacheDirectory, cacheFileName);
            var prepared = new PreparedAsset(asset, cacheFilePath, 0, contentHash, false);
            if (AssetBuildPathUtility.IsModelPath(asset.FullPath))
            {
                modelsToConvert.Add(i);
            }
            else
            {
                prepared = ConvertAsset(engineApi, prepared);
                ReportCompleted(asset);
            }
            preparedAssets.Add(prepared);
        }

        foreach (var batch in modelsToConvert.Chunk(MODEL_WORKER_COUNT))
        {
            var tasks = batch.Select(index => Task.Run(() => ConvertAsset(engineApi, preparedAssets[index]))).ToArray();
            // WhenAll drains the whole batch even on failure, before the session can unload its DLL.
            var converted = Task.WhenAll(tasks).GetAwaiter().GetResult();
            for (var i = 0; i < batch.Length; i++)
            {
                preparedAssets[batch[i]] = converted[i];
                ReportCompleted(converted[i].Asset);
            }
        }

        foreach (var prepared in preparedAssets.Where(asset => !asset.CacheHit && asset.SizeBytes > 0))
        {
            var asset = prepared.Asset;
            var entry = _cacheService.CreateEntry(asset, prepared.ContentHash, Path.GetFileName(prepared.CacheFilePath), prepared.SizeBytes);
            _cacheService.AddEntry(manifest, entry);
            report.BuiltAssets.Add(new AssetsBuildEntryReport
            {
                AssetId = asset.Meta.AssetId,
                AssetPath = asset.FullPath,
                BuildMs = prepared.BuildMs,
                SizeBytes = prepared.SizeBytes
            });
        }
        return preparedAssets;

        void ReportCompleted(AssetInfo asset) => onAssetBuilding?.Invoke(new AssetBuildProgressInfo(++completed, assets.Count, asset.FullPath));
    }

    private PreparedAsset ConvertAsset(IEngineApi engineApi, PreparedAsset asset)
    {
        var stopwatch = Stopwatch.StartNew();
        var sizeBytes = BuildAssetToCache(engineApi, asset.Asset.FullPath, asset.CacheFilePath);
        return asset with { SizeBytes = sizeBytes, BuildMs = stopwatch.ElapsedMilliseconds };
    }

    private static BuildAssetMap PackAssets(IEnumerable<PreparedAsset> assets, string assetsBinPath, AssetsBuildCacheReport report)
    {
        const string INNER_PATH = "assets.bin";
        var map = new BuildAssetMap();
        using var assetsStream = new FileStream(assetsBinPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        foreach (var prepared in assets)
        {
            var asset = prepared.Asset;
            var offset = assetsStream.Position;
            if (prepared.SizeBytes > 0)
            {
                using var cacheStream = new FileStream(prepared.CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                cacheStream.CopyTo(assetsStream);
            }
            map.Add(new BuildAssetMap.AssetBuildInfo(asset.Meta.AssetId, Path.GetFileName(asset.FullPath), asset.FullPath, INNER_PATH, offset));
        }
        report.TotalBytes = assetsStream.Position;
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

}
