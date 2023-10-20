using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Assets;

public class AssetsService : IAssetsService
{
	public Utils.Common.IObservable<bool> SaveInProcess => _saveInProcess;

	private readonly Observable<bool> _saveInProcess = new(false);

	private readonly Dictionary<string, AssetInfo> _idToAssetInfoMap = new();
	private readonly Dictionary<string, Asset> _idToAssetMap = new();
	private readonly Dictionary<Asset, AssetInfo> _assetToAssetInfoMap = new();

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
		await ImportAssets();
		
		_logger.LogWarning("Refreshing assets");
		
		var projectRoot = _resourceService.GetFullPath();

		_idToAssetInfoMap.Clear();
		foreach (var file in Directory.EnumerateFiles(projectRoot, $"*{FileExtensions.META}", SearchOption.AllDirectories))
		{
			try
			{
				var meta = await _resourceService.Load<AssetMeta>(file);
				if (meta == null)
				{
					File.Delete(file);
					continue;
				}

				var assetExtension = AssetUtils.GetExtensionForAssetType(meta.Type);
				var assetPath = file.Replace(FileExtensions.META, assetExtension);
				if (!File.Exists(assetPath))
				{
					File.Delete(file);
					continue;
				}
				
				var assetInfo = new AssetInfo(meta, assetPath);
				_idToAssetInfoMap[meta.Id] = assetInfo;
				_logger.Log(assetInfo.ToString());
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		}
		
		_logger.Log($"Total assets found: {_idToAssetInfoMap.Count}");
	}

	public Task<bool> Create(Asset asset, string projectPath) => Create(asset, AllocateAssetId(), projectPath);

	public async Task<bool> Create(Asset asset, string id, string projectPath)
	{
		try
		{
			var fullPath = _resourceService.GetFullPath(projectPath);
			var extension = Path.GetExtension(fullPath);
			if (extension == null) throw new Exception($"Project path {projectPath} is missing extension");
			if (_resourceService.Exists(fullPath)) throw new Exception($"Cannot create asset because another file exists at {fullPath}");
			
			var data = _serializer.Serialize(asset);
			if (!await _resourceService.Write(data, fullPath)) throw new Exception("Asset creation failed");

			var meta = new AssetMeta(id, AssetUtils.GetAssetType(asset));
			var metaPath = fullPath.Replace(extension, FileExtensions.META);
			await CreateMetaFile(meta, metaPath);
			
			var assetInfo = new AssetInfo(meta, fullPath);
			_idToAssetInfoMap[meta.Id] = assetInfo;
			AddToLoadedAssets(asset, assetInfo);
			
			return true;
		}
		catch (Exception e)
		{
			_logger.LogException(e);
		}
		
		return false;
	}

	public bool Exists<T>(string assetId) where T : Asset => _idToAssetInfoMap.ContainsKey(assetId) && _idToAssetInfoMap[assetId].GetType() == typeof(T);

	public async Task<T?> Load<T>(string assetId) where T : Asset
	{
		if (_idToAssetMap.ContainsKey(assetId)) return (T) _idToAssetMap[assetId];
		
		if (!_idToAssetInfoMap.ContainsKey(assetId)) return null;
		var assetInfo = _idToAssetInfoMap[assetId];
		var asset = await _resourceService.Load<T>(assetInfo.FullPath);
		
		if (asset != null)
		{
			AddToLoadedAssets(asset, assetInfo);
		}
		
		return asset;
	}

	public async Task<T?> LoadFrom<T>(string projectPath) where T : Asset
	{
		var fullPath = _resourceService.GetFullPath(projectPath);
		
		if (!_resourceService.Exists(fullPath)) return null;
		
		var assetPath = _idToAssetInfoMap.FirstOrDefault(x => x.Value.FullPath == fullPath);
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
			foreach (var loadedAsset in _idToAssetMap)
			{
				try
				{
					var data = _serializer.Serialize(loadedAsset.Value);
					var path = _idToAssetInfoMap[loadedAsset.Key].FullPath;
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

	public AssetInfo GetAssetInfo(Asset asset) => _assetToAssetInfoMap[asset];
	public string GetAssetId(Asset asset) => GetAssetInfo(asset).Meta.Id;

	public async Task<Asset> LoadAsset(AssetInfo assetInfo)
	{
		// using reflection to load assets by their type 
		var loadMethod = typeof(AssetsService).GetMethod(nameof(Load));
		var genericMethod = loadMethod!.MakeGenericMethod(AssetUtils.GetAssetType(assetInfo.Meta.Type));
		var task = (Task) genericMethod.Invoke(this, new[] { assetInfo.Meta.Id })!;
		await task;
		var result = task.GetType().GetProperty("Result");
		var asset = (Asset) result!.GetValue(task)!;
		if (asset == null) throw new Exception($"Could not load asset with id {assetInfo.Meta.Id} of type {assetInfo.Meta.Type}");

		return asset;
	}
	
	// todo: track build dirty assets
	public IEnumerable<AssetInfo> GetBuildDirtyAssets() => _idToAssetInfoMap.Values;

	private async Task ImportAssets()
	{
		_logger.LogWarning("Importing assets");
		
		var projectRoot = _resourceService.GetFullPath();

		var counter = 0;
		foreach (var file in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
		{
			try
			{
				var extension = Path.GetExtension(file);
				if (string.IsNullOrEmpty(extension)) continue;
				if (!AssetUtils.TryGetAssetType(extension, out var assetType)) continue;
			
				var metaFilePath = file.Replace(extension, FileExtensions.META);
				var metaFileExists = File.Exists(metaFilePath);
				if (metaFileExists) continue;

				_logger.Log($"Importing {file}");
				
				var meta = new AssetMeta(AllocateAssetId(), assetType);
				await CreateMetaFile(meta, metaFilePath);
				
				counter += 1;
			}
			catch (Exception e)
			{
				_logger.LogException(e);
			}
		}
		
		_logger.Log($"Imported {counter} assets");
	}

	private void AddToLoadedAssets(Asset asset, AssetInfo assetInfo)
	{
		if (_idToAssetMap.ContainsKey(assetInfo.Meta.Id)) throw new Exception($"Asset already exists in {nameof(_idToAssetMap)}");
		if (_assetToAssetInfoMap.ContainsKey(asset)) throw new Exception($"Asset already exists in {nameof(_assetToAssetInfoMap)}");
		
		_idToAssetMap.Add(assetInfo.Meta.Id, asset);
		_assetToAssetInfoMap.Add(asset, assetInfo);
	}

	private async Task CreateMetaFile(AssetMeta meta, string fullPath)
	{
		var didCreate = await _resourceService.Write(_serializer.Serialize(meta), fullPath);
		if (!didCreate) throw new Exception("Asset Meta file creation failed");
	}

	private static string AllocateAssetId() => new(Guid.NewGuid().ToString());
}