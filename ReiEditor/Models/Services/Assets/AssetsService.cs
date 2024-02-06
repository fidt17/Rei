using System;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
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

    private readonly ILogger<AssetsService> _logger;
    private readonly IResourceService _resourceService;
    private readonly ISerializer _serializer;
    private readonly IActiveProjectService _activeProject;
    private readonly IEditorProceduresService _editorProceduresService;
    private readonly IAssetRegistry _assetRegistry;

    public AssetsService(
        ILogger<AssetsService> logger, 
        IResourceService resourceService, 
        ISerializer serializer, 
        IActiveProjectService activeProject, 
        IEditorProceduresService editorProceduresService, 
        IAssetRegistry assetRegistry)
    {
        _logger = logger;
        _resourceService = resourceService;
        _serializer = serializer;
        _activeProject = activeProject;
        _editorProceduresService = editorProceduresService;
        _assetRegistry = assetRegistry;
    }

    public async Task<T?> Load<T>(string assetId) where T : Asset
    {
        if (_assetRegistry.TryGetLoadedAsset(assetId, out var loadedAsset)) return (T?) loadedAsset;
        if (!_assetRegistry.TryGetById(assetId, out var assetInfo)) return null;
		
        return await Load<T>(assetInfo);
    }

    public async Task<T?> LoadFrom<T>(string projectPath) where T : Asset
    {
        try
        {
            var meta = await _resourceService.Load<AssetMeta>(projectPath + FileExtensions.META);
            return await Load<T>(meta.AssetId);
        }
        catch (Exception)
        {
            // meta is missing
        }
        
        var fullPath = _resourceService.GetProjectPath(projectPath);
        if (!_assetRegistry.TryGetByPath(fullPath, out var assetInfo)) return null;

        return await Load<T>(assetInfo);
    }

    public async Task<T?> Load<T>(AssetInfo assetInfo) where T : Asset
    {
        var asset = await _resourceService.TryLoad<T>(assetInfo.FullPath);
		
        if (asset != null)
        {
            _assetRegistry.AddToLoadedAssets(assetInfo, asset);
        }
		
        return asset;
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
		
            foreach (var asset in _assetRegistry.GetDirtyAssets())
            {
                try
                {
                    var data = _serializer.Serialize(asset);
                    await _resourceService.Write(data, asset.FullPath);
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
}