using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets;

public class AssetsService : IAssetsService
{
	private readonly Dictionary<string, AssetPath> _assetsMap = new();
	private readonly Dictionary<string, Asset> _loadedAssets = new();

	private readonly ILogger<AssetsService> _logger;
	private readonly IResourceService _resourceService;
	private readonly ISerializer _serializer;
	private readonly IActiveProjectService _activeProject;

	public AssetsService(ILogger<AssetsService> logger, IResourceService resourceService, ISerializer serializer, IActiveProjectService activeProject)
	{
		_logger = logger;
		_resourceService = resourceService;
		_serializer = serializer;
		_activeProject = activeProject;
	}
	
	public async Task RefreshAssets()
	{
		_logger.LogWarning("Refreshing assets");
		
		var projectRoot = _resourceService.GetFullPath();

		var extensionToTypeMap = new Dictionary<string, Type>
		{
			{ FileExtensions.SCENE, typeof(Scene) }
		};
		
		_assetsMap.Clear();
		foreach (var file in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
		{
			var extension = Path.GetExtension(file);
			if (string.IsNullOrEmpty(extension)) continue;
			if (!extensionToTypeMap.ContainsKey(extension)) continue;
			
			try
			{
				var asset = await _resourceService.Load<Asset>(file);
				if (asset == null) throw new Exception($"Could not deserialize asset at path {file}");
					
				var id = asset.Id;
				_assetsMap[id] = new AssetPath(file, extensionToTypeMap[extension]);
				_logger.Log($"Asset [{id}] {extensionToTypeMap[extension]}");
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		}
		
		_logger.Log($"Total assets found: {_assetsMap.Count}");
	}

	public string AllocateAssetId() => new(Guid.NewGuid().ToString());

	public Task<bool> Create(Asset asset, string projectPath)
	{
		try
		{
			var fullPath = _resourceService.GetFullPath(projectPath, $"{asset.Name}{AssetUtils.AssetFileExtensions[asset.GetType()]}");
			if (_resourceService.Exists(fullPath)) throw new Exception($"Cannot create asset because another file exists at {fullPath}");
			
			var data = _serializer.Serialize(asset);
			
			Directory.CreateDirectory(_resourceService.GetFullPath(projectPath));
			return _resourceService.Write(data, fullPath);
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		return Task.FromResult(false);
	}

	public bool Exists<T>(string assetId) where T : Asset => _assetsMap.ContainsKey(assetId) && _assetsMap[assetId].GetType() == typeof(T);

	public async Task<T?> Load<T>(string assetId) where T : Asset
	{
		if (_loadedAssets.ContainsKey(assetId)) return (T) _loadedAssets[assetId];
		
		if (!_assetsMap.ContainsKey(assetId)) return null;
		var asset = await _resourceService.Load<T>(_assetsMap[assetId].FullPath);
		
		if (asset != null)
		{
			_loadedAssets.Add(asset.Id, asset);
		}
		
		return asset;
	}

	public async Task SaveProject()
	{
		var project = _activeProject.GetActiveProject();
		project.SetProjectLastEditTime(DateTime.Now);
		await File.WriteAllTextAsync(project.ProjectFilePath, _serializer.Serialize(project));
		
		// todo: save dirty assets
	}

	public Task<IEnumerable<AssetPath>> GetBuildDirtyAssets()
	{
		// todo: track build dirty assets
		return Task.FromResult<IEnumerable<AssetPath>>(_assetsMap.Values);
	}
}