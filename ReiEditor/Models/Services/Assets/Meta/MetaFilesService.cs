using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets.Meta;

public class MetaFilesService : IMetaFilesService
{
    private readonly IResourceService _resourceService;
    private readonly ISerializer _serializer;
    private readonly ILogger<MetaFilesService> _logger;

    public MetaFilesService(
        IResourceService resourceService,
        ISerializer serializer, 
        ILogger<MetaFilesService> logger)
    {
        _resourceService = resourceService;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath)
    {
        var metaPath = assetPath + FileExtensions.META;
        var didCreate = await _resourceService.Write(_serializer.Serialize(meta), metaPath);
        if (!didCreate) throw new Exception("Asset Meta file creation failed");

        return new ObjectFile<AssetMeta>(meta, metaPath);
    }

    public async Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy)
    {
        await RegenerateMetaFilesForTargetsInternal(targets, policy);
    }

    public async Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy)
    {
        await RegenerateMetaFilesInDirectoryInternal(directoryPath, policy);
    }

    public async Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy)
    {
        await RegenerateMetaFileForAssetInternal(assetPath, policy);
    }

    public void DeleteMetaFile(string assetPath)
    {
        var metaPath = assetPath + FileExtensions.META;
        if (File.Exists(metaPath))
        {
            _logger.Log("Deleting meta file: " + metaPath + "");
            File.Delete(metaPath);
        }
    }
    
    public async Task DeleteInvalidMetaFiles()
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
                DeleteMetaFile(assetPath);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }
        
        _logger.Log($"Total invalid meta files deleted: {deletedCounter}");
    }

    public void MoveMetaFile(string oldAssetPath, string newAssetPath)
    {
        var oldMetaPath = oldAssetPath + FileExtensions.META;
        var newMetaPath = newAssetPath + FileExtensions.META;
        if (File.Exists(oldMetaPath))
        {
            if (File.Exists(newMetaPath))
            {
                File.Delete(newMetaPath);
            }
            File.Move(oldMetaPath, newMetaPath);
        }
    }

    private async Task RegenerateMetaFilesForTargetsInternal(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy)
    {
        foreach (var target in targets)
        {
            if (Directory.Exists(target))
            {
                await RegenerateMetaFilesInDirectoryInternal(target, policy);
                continue;
            }

            await RegenerateMetaFileForAssetInternal(target, policy);
        }
    }

    private async Task RegenerateMetaFilesInDirectoryInternal(string directoryPath, IMetaFileRegenerationPolicy policy)
    {
        if (!Directory.Exists(directoryPath)) return;

        var assetFiles = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(file => AssetImportUtils.IsValidAssetExtensionForMetaFile(Path.GetExtension(file)))
            .ToList();

        foreach (var assetPath in assetFiles)
        {
            await RegenerateMetaFileForAssetInternal(assetPath, policy);
        }
    }

    private async Task RegenerateMetaFileForAssetInternal(string assetPath, IMetaFileRegenerationPolicy policy)
    {
        if (!File.Exists(assetPath)) return;
        if (!AssetImportUtils.IsValidAssetExtensionForMetaFile(Path.GetExtension(assetPath))) return;

        var existingMeta = await _resourceService.TryLoad<AssetMeta>(assetPath + FileExtensions.META);
        var newAssetId = ReiAssetIdUtility.TryCreateFromAssetPath(assetPath, _resourceService, out var engineResourceAssetId)
            ? engineResourceAssetId
            : Guid.NewGuid().ToString();
        var meta = existingMeta?.CreateCopyWithId(newAssetId) ?? new AssetMeta(newAssetId);

        policy.Apply(meta, assetPath);

        await CreateMetaFile(meta, assetPath);
    }
}
