using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Assets.Import;

public class AssetImporter : IAssetImporter
{
    public event Action? ImportedAssetsEvent;

    public Utils.Common.IObservable<bool> IsImporting => _isImporting;

    private readonly Observable<bool> _isImporting = new(false);

    private readonly ILogger<AssetImporter> _logger;
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetsService _assetsService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IShaderRegistry _shaderRegistry;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourFileUtility _behaviourFileUtility;
    private readonly ISerializer _serializer;
    private readonly IAssetSerializerMigrationService _assetSerializerMigrationService;
    private readonly IEditorProceduresService _editorProceduresService;

    public AssetImporter(
        ILogger<AssetImporter> logger,
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IMetaFilesService metaFilesService,
        IBehaviourRegistry behaviourRegistry, 
        IShaderRegistry shaderRegistry,
        IAssetRegistry assetRegistry, 
        IBehaviourComponentsService behaviourComponentsService, 
        IBehaviourFileUtility behaviourFileUtility,
        ISerializer serializer, 
        IAssetSerializerMigrationService assetSerializerMigrationService,
        IAssetsService assetsService,
        IEditorProceduresService editorProceduresService)
    {
        _logger = logger;
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _metaFilesService = metaFilesService;
        _behaviourRegistry = behaviourRegistry;
        _shaderRegistry = shaderRegistry;
        _assetRegistry = assetRegistry;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourFileUtility = behaviourFileUtility;
        _serializer = serializer;
        _assetSerializerMigrationService = assetSerializerMigrationService;
        _assetsService = assetsService;
        _editorProceduresService = editorProceduresService;
    }

    public async Task<List<AssetInfo>> ReimportAll()
    {
        var procedure = TryBeginImportProcedure();
        if (procedure == null) return new List<AssetInfo>();

        var assets = new List<AssetInfo>();
        
        try
        {
            await _metaFilesService.DeleteInvalidMetaFiles();
            
            assets = await ImportAssets();
            _assetRegistry.UpdateRegistry(assets);
            
            await _behaviourRegistry.RefreshBehaviours();
            await _shaderRegistry.RefreshShaders();
            await ImportScenes();
        }
        catch (Exception e)
        {
            _logger.LogError($"Caught exception during import: {e.Message}");
        }
        
        EndImportProcedure(procedure);
        
        return assets;
    }

    public async Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths)
    {
        var targetFiles = FileExtensions.FindAllFilesIn(paths);
        if (targetFiles.Count == 0) return new List<AssetInfo>();

        var procedure = TryBeginImportProcedure();
        if (procedure == null) return new List<AssetInfo>();
        
        var importedAssets = new List<AssetInfo>();

        try
        {
            foreach (var assetPath in targetFiles)
            {
                try
                {
                    if (!AssetImportUtils.IsValidAssetExtensionForMetaFile(Path.GetExtension(assetPath))) continue;

                    var metaFilePath = assetPath + FileExtensions.META;
                    AssetMeta? meta;

                    if (File.Exists(metaFilePath))
                    {
                        meta = await _resourceService.TryLoad<AssetMeta>(metaFilePath);
                        if (meta is null)
                        {
                            File.Delete(metaFilePath);
                            meta = new AssetMeta(ResolveAssetId(assetPath));
                            await _metaFilesService.CreateMetaFile(meta, assetPath);
                        }
                        else
                        {
                            meta = await EnsureEngineResourceAssetId(meta, assetPath);
                        }
                    }
                    else
                    {
                        meta = new AssetMeta(ResolveAssetId(assetPath));
                        await _metaFilesService.CreateMetaFile(meta, assetPath);
                    }

                    importedAssets.Add(new AssetInfo(meta, assetPath));
                    await _assetSerializerMigrationService.TryMigrateAssetFile(assetPath);
                }
                catch (Exception e)
                {
                    _logger.LogException(e);
                }
            }

            _assetRegistry.RegisterNewAssets(importedAssets);

            if (targetFiles.Any(f => Path.GetExtension(f) == FileExtensions.SCENE))
            {
                await ImportScenes();
            }

            bool isAnyBehaviour = false;
            foreach (var targetFile in targetFiles)
            {
                if (!await _behaviourFileUtility.IsBehaviourFile(targetFile)) continue;
                
                isAnyBehaviour = true;
                break;
            }

            if (isAnyBehaviour)
            {
                await _behaviourRegistry.RefreshBehaviours();
            }

            var isAnyShader = targetFiles.Any(x => Path.GetExtension(x).Equals(FileExtensions.RSHADER, StringComparison.OrdinalIgnoreCase));
            if (isAnyShader)
            {
                await _shaderRegistry.RefreshShaders();
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"Caught exception during import: {e.Message}");
        }
        
        EndImportProcedure(procedure);
        
        return importedAssets;
    }

    private Procedure? TryBeginImportProcedure()
    {
        if (_isImporting) return null;
            
        _logger.Log("Starting assets import");
            
        _isImporting.Value = true;
        
        var procedure = new Procedure("Importing assets");
        _editorProceduresService.TrackProcedure(procedure);
        
        return procedure;
    }

    private void EndImportProcedure(Procedure procedure)
    {
        _logger.Log("Asset import complete");
        
        procedure.Complete();
        
        if (!_isImporting) return;
        _isImporting.Value = false;
        
        ImportedAssetsEvent?.Invoke();
    }

    private async Task<List<AssetInfo>> ImportAssets()
    {
        _logger.Log("Importing assets...");
        var projectRoot = _resourceService.GetProjectPath();

        var importedAssets = new List<AssetInfo>();
        foreach (var assetPath in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                if (!AssetImportUtils.IsValidAssetExtensionForMetaFile(Path.GetExtension(assetPath))) continue;
                
                var metaFilePath = assetPath + FileExtensions.META;
                var metaFileExists = File.Exists(metaFilePath);
                
                if (!metaFileExists)
                {
                    var meta = new AssetMeta(ResolveAssetId(assetPath));
                    await _metaFilesService.CreateMetaFile(meta, assetPath);
                    importedAssets.Add(new AssetInfo(meta, assetPath));
                }
                else
                {
                    var meta = await _resourceService.TryLoad<AssetMeta>(metaFilePath);
                    if (meta == null) throw new Exception($"Tried to load invalid meta at {metaFilePath}");
                    meta = await EnsureEngineResourceAssetId(meta, assetPath);
                    importedAssets.Add(new AssetInfo(meta, assetPath));
                }
                
                await _assetSerializerMigrationService.TryMigrateAssetFile(assetPath);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }

        return importedAssets;
    }

    private string ResolveAssetId(string assetPath)
    {
        return ReiAssetIdUtility.TryCreateFromAssetPath(assetPath, _resourceService, out var engineResourceAssetId)
            ? engineResourceAssetId
            : _assetCreator.AllocateAssetId();
    }

    private async Task<AssetMeta> EnsureEngineResourceAssetId(AssetMeta meta, string assetPath)
    {
        if (!ReiAssetIdUtility.TryCreateFromAssetPath(assetPath, _resourceService, out var expectedAssetId)) return meta;
        if (meta.AssetId == expectedAssetId) return meta;

        var updatedMeta = meta.CreateCopyWithId(expectedAssetId);
        await _metaFilesService.CreateMetaFile(updatedMeta, assetPath);
        return updatedMeta;
    }

    private async Task ImportScenes()
    {
        foreach (var sceneFilePath in _resourceService.GetAllWithExtension(FileExtensions.SCENE))
        {
            var scene = await _assetsService.LoadFrom<Scene>(sceneFilePath);
            
            if (scene == null)
            {
                _logger.LogWarning($"Could not load Scene asset from {sceneFilePath}");
                continue;
            }
            
            foreach (var sceneEntity in scene.Entities)
            {
                _behaviourComponentsService.RefreshComponents(sceneEntity);
            }

            var data = _serializer.Serialize(scene);
            await _resourceService.Write(data, sceneFilePath);
        }
    }
}
