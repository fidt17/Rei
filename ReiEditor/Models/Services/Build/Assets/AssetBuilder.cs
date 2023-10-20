using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuilder : IAssetBuilder
{
	private readonly IAssetsService _assetsService;
	private readonly IResourceService _resourceService;
	private readonly IBinarySerializer _binarySerializer;
	
	public AssetBuilder(IBinarySerializer binarySerializer, IAssetsService assetsService, IResourceService resourceService)
	{
		_binarySerializer = binarySerializer;
		_assetsService = assetsService;
		_resourceService = resourceService;
	}

	public async Task BuildAssets(string buildFolder)
	{
		var assets = _assetsService.GetBuildDirtyAssets();
		if (Directory.Exists(buildFolder))
		{
			Directory.Delete(buildFolder, true);
		}
    		
		var map = await Build(assets, buildFolder, "assets");
		await Build(map, buildFolder, "map");
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
		
		await using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write);
		await using var writer = new BinaryWriter(stream, Encoding.UTF8, false);

		var offset = 0L;
		foreach (var assetInfo in assetInfos)
		{
			var assetType = AssetUtils.GetAssetType(assetInfo.Meta.Type);
			object? asset;
			if (assetType.IsAssignableTo(typeof(Asset)))
			{
				asset = await _assetsService.LoadAsset(assetInfo);
			}
			else
			{
				asset = await _resourceService.Load<object>(assetInfo.FullPath);
			}

			if (asset == null) throw new Exception($"Could not load asset from {assetInfo.FullPath} of type {assetInfo.Meta.Type}-{assetType}");
			
			var method = typeof(IBinarySerializer).GetMethod(nameof(IBinarySerializer.Serialize));
			var generic = method!.MakeGenericMethod(assetType);
			generic.Invoke(_binarySerializer, new []{asset, (object)writer});
			
			map.Add(new BuildAssetMap.AssetBuildInfo(assetInfo.Meta.Id, innerPath, offset));
			offset = writer.BaseStream.Length;
		}

		return map;
	}
}