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
	private readonly Dictionary<Type, Func<BinaryWriter, AssetPath, Task>> _builderMap;
	private readonly IResourceService _resourceService;
	
	public AssetBuilder(IResourceService resourceService)
	{
		_resourceService = resourceService;
		_builderMap = new Dictionary<Type, Func<BinaryWriter, AssetPath, Task>>
		{
			{
				typeof(Scene), async (writer, assetPath) =>
				{
					var asset = await _resourceService.Load<Scene>(assetPath.FullPath);
					if (asset == null) throw new Exception($"Could not load asset. {assetPath.FullPath}");
					SceneAssetBuilder.Build(writer, asset);
				}
			}
		};
	}

	public async Task Build(AssetPath assetPath, string buildDir)
	{
		var assetType = assetPath.AssetType;
		if (!_builderMap.ContainsKey(assetType)) throw new Exception("Unsupported asset type");

		var path = Path.Combine(buildDir, "Resources", GetAssetBuildDir(assetType));
		Directory.CreateDirectory(path);
		
		var name = Path.GetFileNameWithoutExtension(assetPath.FullPath);
		path = Path.Combine(path, $"{name}.bin");
		
		await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
		await using var writer = new BinaryWriter(stream, Encoding.UTF8, false);
		
		await _builderMap[assetType](writer, assetPath);
	}

	private static string GetAssetBuildDir(Type assetType)
	{
		if (assetType == typeof(Scene))
		{
			return "Scenes";
		}

		return "";
	}
}
