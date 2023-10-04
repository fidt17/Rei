using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuilder : IAssetBuilder
{
	private readonly Dictionary<Type, Func<BinaryWriter, AssetInfo, Task>> _builderMap;
	private readonly IResourceService _resourceService;
	
	public AssetBuilder(IResourceService resourceService)
	{
		_resourceService = resourceService;
		_builderMap = ConfigureBuilderMap();
	}

	public async Task Build(AssetInfo assetInfo, string buildDir)
	{
		var assetType = assetInfo.AssetType;
		if (!_builderMap.ContainsKey(assetType)) throw new Exception("Unsupported asset type");

		var path = Path.Combine(buildDir, "Resources");
		Directory.CreateDirectory(path);
		path = Path.Combine(path, $"{GetAssetName(assetInfo)}.bin");
		
		await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
		await using var writer = new BinaryWriter(stream, Encoding.UTF8, false);
		
		await _builderMap[assetType](writer, assetInfo);
	}

	private string GetAssetName(AssetInfo assetInfo)
	{
		if (assetInfo.AssetType == typeof(BuildScenesConfiguration)) return "build_scenes";

		return $"{assetInfo.Id}";
	}

	private Dictionary<Type, Func<BinaryWriter, AssetInfo, Task>> ConfigureBuilderMap()
	{
		return new Dictionary<Type, Func<BinaryWriter, AssetInfo, Task>>
		{
			{
				typeof(Scene), async (writer, assetPath) =>
				{
					var asset = await _resourceService.Load<Scene>(assetPath.FullPath);
					if (asset == null) throw new Exception($"Could not load asset. {assetPath.FullPath}");
					SceneAssetBuilder.Build(writer, asset);
				}
			},
			
			{
				typeof(BuildScenesConfiguration), async (writer, assetPath) =>
				{
					var asset = await _resourceService.Load<BuildScenesConfiguration>(assetPath.FullPath);
					if (asset == null) throw new Exception($"Could not load asset. {assetPath.FullPath}");
					BuildScenedConfigurationAssetBuilder.Build(writer, asset);
				}
			}
		};
	}
}
