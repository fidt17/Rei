using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets;

public class AssetImporter : IAssetImporter
{
    public event Action? ImportedAssetsEvent;

    private readonly ILogger<AssetImporter> _logger;
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetsService _assetsService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly ISerializer _serializer;

    public AssetImporter(
        ILogger<AssetImporter> logger,
        IResourceService resourceService,
        IAssetCreator assetCreator,
        IBehaviourRegistry behaviourRegistry, 
        IAssetRegistry assetRegistry, 
        IBehaviourComponentsService behaviourComponentsService, 
        ISerializer serializer, 
        IAssetsService assetsService)
    {
        _logger = logger;
        _resourceService = resourceService;
        _assetCreator = assetCreator;
        _behaviourRegistry = behaviourRegistry;
        _assetRegistry = assetRegistry;
        _behaviourComponentsService = behaviourComponentsService;
        _serializer = serializer;
        _assetsService = assetsService;
    }

    public async Task<List<AssetInfo>> ReimportAll()
    {
        await DeleteInvalidMetaFiles();
        
        var assets = await ImportAssets();
        _assetRegistry.RegisterAssets(assets);
        
        await _behaviourRegistry.RefreshBehaviours();
        await ImportScenes();

        ImportedAssetsEvent?.Invoke();

        return assets;
    }

    private async Task DeleteInvalidMetaFiles()
    {
        _logger.Log("Locating invalid meta files...");
        
        var deletedCounter = 0;
        foreach (var file in Directory.EnumerateFiles(_resourceService.GetProjectPath(), $"*{FileExtensions.META}", SearchOption.AllDirectories))
        {
            try
            {
                var assetPath = file.Replace(FileExtensions.META, "");
                if (File.Exists(assetPath) && await _resourceService.TryLoad<AssetMeta>(file) != null) continue;
                
                deletedCounter++;
                File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }
        
        _logger.Log($"Total invalid meta files deleted: {deletedCounter}");
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
                if (!IsValidAssetExtensionForMetaFile(Path.GetExtension(assetPath))) continue;
                
                var metaFilePath = assetPath + FileExtensions.META;
                var metaFileExists = File.Exists(metaFilePath);
                
                if (!metaFileExists)
                {
                    var meta = new AssetMeta(_assetCreator.AllocateAssetId());
                    await _assetCreator.CreateMetaFile(meta, assetPath);
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

    private bool IsValidAssetExtensionForMetaFile(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        return extension is not (
            FileExtensions.META or 
            FileExtensions.CPP or 
            FileExtensions.VS_PROJECT);
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