using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
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
        var counter = 0;
        foreach (var file in Directory.EnumerateFiles(_resourceService.GetProjectPath(), $"*{FileExtensions.META}", SearchOption.AllDirectories))
        {
            try
            {
                var meta = await _resourceService.Load<AssetMeta>(file);
                if (meta == null)
                {
                    counter++;
                    File.Delete(file);
                    continue;
                }
                
                var assetExtension = AssetUtils.GetExtensionForAssetType(meta.Type);
                var assetPath = file.Replace(FileExtensions.META, assetExtension);
                if (File.Exists(assetPath)) continue;
                
                File.Delete(file);
                counter++;
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }

        return counter;
    }

    public async Task<List<AssetInfo>> ImportAssets()
    {
        _logger.Log("Import assets");
        var projectRoot = _resourceService.GetProjectPath();

        var importedAssets = new List<AssetInfo>();
        foreach (var file in Directory.EnumerateFiles(projectRoot, "*.*", SearchOption.AllDirectories))
        {
            try
            {
                var extension = Path.GetExtension(file);
                if (string.IsNullOrEmpty(extension)) continue;
                if (!AssetUtils.TryGetAssetType(extension, out var assetType)) continue;
			
                var metaFilePath = file.Replace(extension, FileExtensions.META);
                var metaFileExists = File.Exists(metaFilePath);
                
                if (!metaFileExists)
                {
                    var meta = new AssetMeta(_assetCreator.AllocateAssetId(), assetType);
                    await _assetCreator.CreateMetaFile(meta, metaFilePath);
                    importedAssets.Add(new AssetInfo(meta, file));
                }
                else
                {
                    var meta = await _resourceService.Load<AssetMeta>(metaFilePath);
                    if (meta == null) throw new Exception($"Tried to load invalid meta at {metaFilePath}");
                    importedAssets.Add(new AssetInfo(meta, file));
                }
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }

        return importedAssets;
    }
}