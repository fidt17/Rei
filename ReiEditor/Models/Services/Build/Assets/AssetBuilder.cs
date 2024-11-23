using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuilder : IAssetBuilder
{
	private readonly IAssetRegistry _assetRegistry;
	private readonly IBinarySerializer _binarySerializer;
	private readonly IEngineApi _engineApi;
	private readonly IClientDllManager _dllManager;
	private readonly ILogger<AssetBuilder> _logger;
	private readonly IEngineLogger _engineLogger;

	public AssetBuilder(IBinarySerializer binarySerializer, IEngineApi engineApi, IClientDllManager dllManager, ILogger<AssetBuilder> logger, IEngineLogger engineLogger, IAssetRegistry assetRegistry)
	{
		_binarySerializer = binarySerializer;
		_engineApi = engineApi;
		_dllManager = dllManager;
		_logger = logger;
		_engineLogger = engineLogger;
		_assetRegistry = assetRegistry;
	}

	public async Task BuildAssets(string buildFolder)
	{
		if (!_dllManager.DllLoaded.Value)
		{
			_dllManager.LoadDll();
		}
		
		_engineLogger.SubscribeToClient();

		var map = await Build(_assetRegistry.GetAllAssets(), buildFolder, "assets");
		await Build(map, buildFolder, "map");
		
		_dllManager.UnloadDll();
	}

	private async Task Build(BuildAssetMap map, string buildDir, string outputName)
	{
		var path = Path.Combine(buildDir, "Resources");
		Directory.CreateDirectory(path);
		path = Path.Combine(path, $"{outputName}.bin");
		
		await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
		await using var writer = new BinaryWriter(stream, Encoding.UTF8, false);

		_binarySerializer.Serialize(map, writer);
		await File.WriteAllTextAsync(path.Replace(".bin", ".json"), JsonConvert.SerializeObject(map, Formatting.Indented));
	}

	private async Task<BuildAssetMap> Build(IEnumerable<AssetInfo> assetInfos, string buildDir, string outputName)
	{
		var map = new BuildAssetMap();

		var innerPath = $"{outputName}.bin";
		var path = Path.Combine(buildDir, "Resources");
		Directory.CreateDirectory(path);
		path = Path.Combine(path, innerPath);
		
		long offset = 0L;
		foreach (var assetInfo in assetInfos)
		{
			var buildTask = Task.Run(() =>
			{
				_logger.Log($"Building asset: {assetInfo.Meta.AssetId}");
				var bytesWritten = _engineApi.BuildAsset(assetInfo.FullPath, path, offset);
				map.Add(new BuildAssetMap.AssetBuildInfo(assetInfo.Meta.AssetId, Path.GetFileName(assetInfo.FullPath), assetInfo.FullPath, innerPath, offset));
				offset += bytesWritten;
			});
			await buildTask;
		}

		return map;
	}
}