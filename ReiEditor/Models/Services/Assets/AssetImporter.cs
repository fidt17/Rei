using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets;

public class AssetImporter : IAssetImporter
{
    private readonly ILogger<AssetImporter> _logger;
    private readonly IResourceService _resourceService;
    private readonly IAssetCreator _assetCreator;

    public AssetImporter(ILogger<AssetImporter> logger, IResourceService resourceService, IAssetCreator assetCreator)
    {
        _logger = logger;
        _resourceService = resourceService;
        _assetCreator = assetCreator;
    }

    public async Task<int> DeleteInvalidMetaFiles()
    {
        _logger.Log("Delete invalid meta files");
        var deletedCounter = 0;
        foreach (var file in Directory.EnumerateFiles(_resourceService.GetProjectPath(), $"*{FileExtensions.META}", SearchOption.AllDirectories))
        {
            try
            {
                var assetPath = file.Replace(FileExtensions.META, "");
                if (File.Exists(assetPath) && await _resourceService.Load<AssetMeta>(file) != null) continue;
                
                deletedCounter++;
                File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }

        return deletedCounter;
    }

    public async Task<List<AssetInfo>> ImportAssets()
    {
        _logger.Log("Import assets");
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
                    var meta = await _resourceService.Load<AssetMeta>(metaFilePath);
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
}