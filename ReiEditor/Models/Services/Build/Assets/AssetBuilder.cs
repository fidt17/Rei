using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuilder : IAssetBuilder
{
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetBuildCachePipeline _assetBuildCachePipeline;
    private readonly IBinarySerializer _binarySerializer;
    private readonly IAssetBuildEngineSessionFactory _engineSessionFactory;
    private readonly ILogger<AssetBuilder> _logger;

    public AssetBuilder(
        IBinarySerializer binarySerializer,
        IAssetBuildEngineSessionFactory engineSessionFactory,
        IAssetRegistry assetRegistry,
        IAssetBuildCachePipeline assetBuildCachePipeline,
        ILogger<AssetBuilder> logger)
    {
        _binarySerializer = binarySerializer;
        _engineSessionFactory = engineSessionFactory;
        _assetRegistry = assetRegistry;
        _assetBuildCachePipeline = assetBuildCachePipeline;
        _logger = logger;
    }

    public async Task BuildAssets(
        BuildExecutionContext buildContext,
        bool forceRebuild = false,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null)
    {
        using var engineSession = string.IsNullOrWhiteSpace(buildContext.ClientDllPath)
            ? _engineSessionFactory.CreateSharedSession()
            : _engineSessionFactory.CreateIsolatedSession(buildContext.ClientDllPath);
        await BuildAssetsInternal(engineSession.EngineApi, buildContext, forceRebuild, onAssetBuilding);
    }

    private async Task SerializeAssetMap(BuildAssetMap map, string buildDir, string outputName)
    {
        var path = Path.Combine(buildDir, ResourceConstants.RESOURCES_DIR_NAME);
        Directory.CreateDirectory(path);
        path = Path.Combine(path, $"{outputName}.bin");
        
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        await using var writer = new BinaryWriter(stream, Encoding.UTF8, false);

        _binarySerializer.Serialize(map, writer);
        await File.WriteAllTextAsync(path.Replace(".bin", ".json"), JsonConvert.SerializeObject(map, Formatting.Indented));
    }

    private void LogBuildSummary(AssetsBuildCacheReport report)
    {
        var msg = new StringBuilder();
        msg.AppendLine();
        msg.AppendLine($"--- Asset build summary ---");
        msg.AppendLine($"Asset build completed in {report.TotalBuildMs} ms");
        msg.AppendLine($"Asset build size: {report.TotalBytes} bytes");

        if (report.BuiltAssets.Count == 0)
        {
            msg.AppendLine("Asset build: no assets rebuilt");
            _logger.Log(msg.ToString());
            return;
        }

        msg.AppendLine("Asset build details:");
        foreach (var built in report.BuiltAssets)
        {
            msg.AppendLine($"  {built.AssetPath} | {built.BuildMs} ms | {built.SizeBytes} bytes");
        }
        
        _logger.Log(msg.ToString());
    }

    private static bool ShouldBuild(AssetInfo assetInfo) => AssetBuildPathUtility.ShouldBuildPath(assetInfo.FullPath);

    private async Task BuildAssetsInternal(
        IEngineApi engineApi,
        BuildExecutionContext buildContext,
        bool forceRebuild,
        Action<AssetBuildProgressInfo>? onAssetBuilding)
    {
        var resourcesDir = buildContext.ResourcesDirectoryPath;
        Directory.CreateDirectory(resourcesDir);

        var assetsBinPath = Path.Combine(resourcesDir, "assets.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(assetsBinPath)!);
        await using (File.Create(assetsBinPath)) { }

        var assets = _assetRegistry.GetAllAssets()
            .Where(ShouldBuild)
            .OrderBy(asset => asset.Meta.AssetId);

        var buildResult = _assetBuildCachePipeline.BuildAssets(
            engineApi,
            assets,
            buildContext.AssetCacheDirectoryPath,
            assetsBinPath,
            forceRebuild,
            onAssetBuilding);
        LogBuildSummary(buildResult.Report);

        await SerializeAssetMap(buildResult.Map, buildContext.BuildFolder, "map");
    }
}
