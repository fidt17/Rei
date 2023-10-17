using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Assets;

public class AssetsService : IAssetsService
{
	public Utils.Common.IObservable<bool> SaveInProcess => _saveInProcess;

	private readonly Observable<bool> _saveInProcess = new(false);

	private readonly Dictionary<string, AssetInfo> _assetsMap = new();
	private readonly Dictionary<string, Asset> _loadedAssets = new();

	private readonly ILogger<AssetsService> _logger;
	private readonly IResourceService _resourceService;
	private readonly ISerializer _serializer;
	private readonly IActiveProjectService _activeProject;
	private readonly IEditorProceduresService _editorProceduresService;

	public AssetsService(ILogger<AssetsService> logger, IResourceService resourceService, ISerializer serializer, IActiveProjectService activeProject, IEditorProceduresService editorProceduresService)
	{
		_logger = logger;
		_resourceService = resourceService;
		_serializer = serializer;
		_activeProject = activeProject;
		_editorProceduresService = editorProceduresService;
	}
	
	public async Task RefreshAssets()
	{
		_logger.LogWarning("Refreshing assets");
		
		var projectRoot = _resourceService.GetFullPath();

		_assetsMap.Clear();
		foreach (var file in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
		{
			var extension = Path.GetExtension(file);
			if (string.IsNullOrEmpty(extension)) continue;
			if (!AssetUtils.AssetFileExtensions.ContainsValue(extension)) continue;
			
			try
			{
				var asset = await _resourceService.Load<Asset>(file);
				if (asset == null) throw new Exception($"Could not deserialize asset at path {file}");
					
				var id = asset.Id;
				_assetsMap[id] = new AssetInfo(id, file, AssetUtils.AssetFileExtensions.First(x => x.Value == extension).Key);
				_logger.Log($"Asset [{id}] {_assetsMap[id].FullPath} {_assetsMap[id].AssetType}");
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		}
		
		_logger.Log($"Total assets found: {_assetsMap.Count}");
	}

	public string AllocateAssetId() => new(Guid.NewGuid().ToString());

	public async Task<bool> Create(Asset asset, string projectPath)
	{
		try
		{
			var fullPath = _resourceService.GetFullPath(projectPath, $"{asset.Name}{AssetUtils.AssetFileExtensions[asset.GetType()]}");
			if (_resourceService.Exists(fullPath)) throw new Exception($"Cannot create asset because another file exists at {fullPath}");
			
			var data = _serializer.Serialize(asset);
			if (!await _resourceService.Write(data, fullPath)) throw new Exception("Asset creation failed");
			
			_assetsMap.Add(asset.Id, new AssetInfo(asset.Id, fullPath, asset.GetType()));
			_loadedAssets.Add(asset.Id, asset);

			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		return false;
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

	public async Task<T?> LoadFrom<T>(string projectPath) where T : Asset
	{
		var fullPath = _resourceService.GetFullPath(projectPath);
		
		if (!_resourceService.Exists(fullPath)) return null;
		
		var assetPath = _assetsMap.FirstOrDefault(x => x.Value.FullPath == fullPath);
		if (assetPath.Value == null) return null;

		return await Load<T>(assetPath.Key);
	}

	public async Task SaveProject()
	{
		if (_saveInProcess) return;
		
		_logger.Log("Save project");
		_saveInProcess.Value = true;
		var saveProcedure = new Procedure("Saving project");
		_editorProceduresService.TrackProcedure(saveProcedure);

		try
		{
			var project = _activeProject.GetActiveProject();
			project.SetProjectLastEditTime(DateTime.Now);
			await _resourceService.Write(_serializer.Serialize(project), project.ProjectFilePath);
		
			// todo: save dirty assets
			foreach (var loadedAsset in _loadedAssets)
			{
				try
				{
					var data = _serializer.Serialize(loadedAsset.Value);
					var path = _assetsMap[loadedAsset.Key].FullPath;
					await _resourceService.Write(data, path);
				}
				catch (Exception e)
				{
					_logger.LogException(e);
				}
			}
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}

		saveProcedure.Complete();
		_saveInProcess.Value = false;
	}

	// todo: track build dirty assets
	public async Task<IEnumerable<Asset>> GetBuildDirtyAssets()
	{
		var assets = new List<Asset>();
		
		foreach (var assetInfo in _assetsMap.Values)
		{
			// using reflection to load assets by their type 
			var loadMethod = typeof(AssetsService).GetMethod(nameof(Load));
			var genericMethod = loadMethod!.MakeGenericMethod(assetInfo.AssetType);
			var task = (Task) genericMethod.Invoke(this, new[] { assetInfo.Id })!;
			await task;
			var result = task.GetType().GetProperty("Result");
			var asset = (Asset) result!.GetValue(task)!;
			if (asset == null) throw new Exception($"Could not load asset with id {assetInfo.Id} of type {assetInfo.AssetType}");
			
			assets.Add(asset);
		}
		
		return assets;
	}
}