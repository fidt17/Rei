using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Behaviours;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Assets;

public class AssetsService : IAssetsService, IDisposable
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
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetImporter _assetImporter;

    public AssetsService(
        ILogger<AssetsService> logger, 
        IResourceService resourceService, 
        ISerializer serializer, 
        IActiveProjectService activeProject, 
        IEditorProceduresService editorProceduresService, 
        IBehaviourComponentsService behaviourComponentsService, 
        IAssetCreator assetCreator, 
        IAssetImporter assetImporter)
    {
        _logger = logger;
        _resourceService = resourceService;
        _serializer = serializer;
        _activeProject = activeProject;
        _editorProceduresService = editorProceduresService;
        _behaviourComponentsService = behaviourComponentsService;
        _assetCreator = assetCreator;
        _assetImporter = assetImporter;

        _assetCreator.AssetCreatedEvent += HandleAssetCreatedEvent;
    }
    
    public void Dispose()
    {
        _assetCreator.AssetCreatedEvent -= HandleAssetCreatedEvent;
    }

    public async Task RefreshAssets()
    {
        _logger.LogWarning("Refreshing assets...");

        var deletedMetaFilesCount = await _assetImporter.DeleteInvalidMetaFiles();
        var assets = await _assetImporter.ImportAssets();
        var behavioursCount = await _behaviourComponentsService.ImportBehaviours();

        _idToAssetInfoMap.Clear();
        foreach (var asset in assets)
        {
            _idToAssetInfoMap[asset.Meta.Id] = asset;
        }
		
        _logger.Log($"Total assets found: {_idToAssetInfoMap.Count}");
        _logger.Log($"Behaviours count: {behavioursCount}");
        _logger.Log($"Deleted invalid meta files: {deletedMetaFilesCount}");
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
        var fullPath = _resourceService.GetProjectPath(projectPath);
		
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

    private void AddToLoadedAssets(Asset asset, AssetInfo assetInfo)
    {
        if (_idToAssetMap.ContainsKey(assetInfo.Meta.Id)) throw new Exception($"Asset already exists in {nameof(_idToAssetMap)}");
        if (_assetToAssetInfoMap.ContainsKey(asset)) throw new Exception($"Asset already exists in {nameof(_assetToAssetInfoMap)}");
		
        _idToAssetMap.Add(assetInfo.Meta.Id, asset);
        _assetToAssetInfoMap.Add(asset, assetInfo);
    }

    private void HandleAssetCreatedEvent(AssetInfo assetInfo, Asset asset)
    {
        _idToAssetInfoMap[assetInfo.Meta.Id] = assetInfo;
        AddToLoadedAssets(asset, assetInfo);
    }
}