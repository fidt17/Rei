using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuilder : IAssetBuilder
{
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetBuildCachePipeline _assetBuildCachePipeline;
    private readonly IBinarySerializer _binarySerializer;
    private readonly IClientDllManager _dllManager;
    private readonly IEngineLogger _engineLogger;
    private readonly ILogger<AssetBuilder> _logger;

    public AssetBuilder(IBinarySerializer binarySerializer, IClientDllManager dllManager, IEngineLogger engineLogger, IAssetRegistry assetRegistry, IAssetBuildCachePipeline assetBuildCachePipeline, ILogger<AssetBuilder> logger)
    {
        _binarySerializer = binarySerializer;
        _dllManager = dllManager;
        _engineLogger = engineLogger;
        _assetRegistry = assetRegistry;
        _assetBuildCachePipeline = assetBuildCachePipeline;
        _logger = logger;
    }

    public async Task BuildAssets(string buildFolder)
    {
        if (!_dllManager.DllLoaded.Value)
        {
            _dllManager.LoadDll();
        }
        
        _engineLogger.SubscribeToClient();

        var resourcesDir = Path.Combine(buildFolder, ResourceConstants.RESOURCES_DIR_NAME);
        Directory.CreateDirectory(resourcesDir);

        var assetsBinPath = Path.Combine(resourcesDir, "assets.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(assetsBinPath)!);
        await using (File.Create(assetsBinPath)) { }

        var assets = _assetRegistry.GetAllAssets()
            .Where(ShouldBuild)
            .OrderBy(asset => asset.Meta.AssetId);
        
        var buildResult = _assetBuildCachePipeline.BuildAssets(assets, buildFolder, assetsBinPath);
        LogBuildSummary(buildResult.Report);
        
        await SerializeAssetMap(buildResult.Map, buildFolder, "map");
        
        _dllManager.UnloadDll();
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

    private static bool ShouldBuild(AssetInfo assetInfo) => ShouldBuildPath(assetInfo.FullPath);

    private static bool ShouldBuildPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        
        var extension = Path.GetExtension(path);
        return extension switch
        {
            FileExtensions.META => false,
            FileExtensions.H => false,
            FileExtensions.CPP => false,
            FileExtensions.VS_PROJECT => false,
            FileExtensions.VS_PROJECT_USER => false,
            FileExtensions.VS_SOLUTION => false,
            _ => true
        };
    }
}