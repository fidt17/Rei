using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Migrations;
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
    private readonly IAssetSerializerMigrationService _assetSerializerMigrationService;
    private readonly IActiveProjectService _activeProject;
    private readonly IEditorProceduresService _editorProceduresService;
    private readonly IAssetRegistry _assetRegistry;

    public AssetsService(
        ILogger<AssetsService> logger,
        IResourceService resourceService,
        ISerializer serializer,
        IAssetSerializerMigrationService assetSerializerMigrationService,
        IActiveProjectService activeProject,
        IEditorProceduresService editorProceduresService,
        IAssetRegistry assetRegistry)
    {
        _logger = logger;
        _resourceService = resourceService;
        _serializer = serializer;
        _assetSerializerMigrationService = assetSerializerMigrationService;
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
        T? asset;
        try
        {
            var sourceJson = await File.ReadAllTextAsync(assetInfo.FullPath);
            var migrationResult = _assetSerializerMigrationService.MigrateAssetJson(typeof(T), sourceJson);
            asset = _serializer.Deserialize<T>(migrationResult.Json);

            if (migrationResult.IsUpdated)
            {
                await _resourceService.Write(migrationResult.Json, assetInfo.FullPath);
            }
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return null;
        }

        if (asset != null)
        {
            _assetRegistry.AddToLoadedAssets(assetInfo, asset);
        }

        return asset;
    }

    public void Unload(string assetId)
    {
        if (!_assetRegistry.TryGetById(assetId, out var assetInfo)) return;

        _assetRegistry.RemoveFromLoadedAssets(assetInfo);
    }

    public async Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions)
    {
        var loadedAssetInfos = _assetRegistry.GetLoadedAssetInfos().ToList();

        foreach (var assetInfo in loadedAssetInfos)
        {
            var extension = Path.GetExtension(assetInfo.FullPath);
            if (ignoredExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;
            if (!_assetRegistry.TryGetLoadedAsset(assetInfo.Meta.AssetId, out var loadedAsset)) continue;

            try
            {
                var jsonData = await File.ReadAllTextAsync(assetInfo.FullPath);
                var migrationResult = _assetSerializerMigrationService.MigrateAssetJson(loadedAsset.GetType(), jsonData);
                JsonConvert.PopulateObject(migrationResult.Json, loadedAsset);

                if (migrationResult.IsUpdated)
                {
                    await _resourceService.Write(migrationResult.Json, assetInfo.FullPath);
                }

                if (loadedAsset is IOnDeserialized onDeserialized)
                {
                    onDeserialized.OnDeserialized();
                }
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }
    }

    public async Task SaveProject()
    {
        if (_saveInProcess) return;

        _logger.Log("Saving project");
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
