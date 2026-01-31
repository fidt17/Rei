using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets.Import;

public class AssetImporter : IAssetImporter
{
    public event Action? ImportedAssetsEvent;

    private readonly ILogger<AssetImporter> _logger;
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetsService _assetsService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourFileUtility _behaviourFileUtility;
    private readonly ISerializer _serializer;

    public AssetImporter(
        ILogger<AssetImporter> logger,
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IMetaFilesService metaFilesService,
        IBehaviourRegistry behaviourRegistry, 
        IAssetRegistry assetRegistry, 
        IBehaviourComponentsService behaviourComponentsService, 
        IBehaviourFileUtility behaviourFileUtility,
        ISerializer serializer, 
        IAssetsService assetsService)
    {
        _logger = logger;
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _metaFilesService = metaFilesService;
        _behaviourRegistry = behaviourRegistry;
        _assetRegistry = assetRegistry;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourFileUtility = behaviourFileUtility;
        _serializer = serializer;
        _assetsService = assetsService;
    }

    public async Task<List<AssetInfo>> ReimportAll()
    {
        await _metaFilesService.DeleteInvalidMetaFiles();
        
        var assets = await ImportAssets();
        _assetRegistry.UpdateRegistry(assets);
        
        await _behaviourRegistry.RefreshBehaviours();
        await ImportScenes();

        ImportedAssetsEvent?.Invoke();

        return assets;
    }

    public async Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths)
    {
        var targetFiles = FileExtensions.FindAllFilesIn(paths);
        if (targetFiles.Count == 0) return new List<AssetInfo>();

        var importedAssets = new List<AssetInfo>();
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
                        meta = new AssetMeta(_assetCreator.AllocateAssetId());
                        await _metaFilesService.CreateMetaFile(meta, assetPath);
                    }
                }
                else
                {
                    meta = new AssetMeta(_assetCreator.AllocateAssetId());
                    await _metaFilesService.CreateMetaFile(meta, assetPath);
                }

                importedAssets.Add(new AssetInfo(meta, assetPath));
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

        if (targetFiles.Any(_behaviourFileUtility.IsBehaviourFile))
        {
            await _behaviourRegistry.RefreshBehaviours();
        }

        ImportedAssetsEvent?.Invoke();
        return importedAssets;
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
                    var meta = new AssetMeta(_assetCreator.AllocateAssetId());
                    await _metaFilesService.CreateMetaFile(meta, assetPath);
                    importedAssets.Add(new AssetInfo(meta, assetPath));
                }
                else
                {
                    var meta = await _resourceService.TryLoad<AssetMeta>(metaFilePath);
                    if (meta == null) throw new Exception($"Tried to load invalid meta at {metaFilePath}");
                    importedAssets.Add(new AssetInfo(meta, assetPath));
                }
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }

        return importedAssets;
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
