using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets.Creation;

public class AssetCreator : IAssetCreator
{
    private readonly IResourceService _resourceService;
    private readonly ISerializer _serializer;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IMetaFilesService _metaFilesService;
    private readonly ILogger<AssetCreator> _logger;

    public AssetCreator(
        IResourceService resourceService,
        ISerializer serializer,
        ILogger<AssetCreator> logger,
        IAssetRegistry assetRegistry,
        IMetaFilesService metaFilesService)
    {
        _resourceService = resourceService;
        _serializer = serializer;
        _logger = logger;
        _assetRegistry = assetRegistry;
        _metaFilesService = metaFilesService;
    }

    public string AllocateAssetId()
    {
        return new(Guid.NewGuid().ToString());
    }

    public Task<bool> Create(Asset asset, string projectPath)
    {
        var assetPath = _resourceService.GetProjectPath(projectPath);
        var assetId = ReiAssetIdUtility.TryCreateFromAssetPath(assetPath, _resourceService, out var engineResourceAssetId)
            ? engineResourceAssetId
            : AllocateAssetId();

        return Create(asset, assetId, projectPath);
    }

    public async Task<bool> Create(Asset asset, string id, string projectPath)
    {
        try
        {
            var assetPath = _resourceService.GetProjectPath(projectPath);
            var extension = Path.GetExtension(assetPath);
            if (extension == null) throw new Exception($"Project path {projectPath} is missing extension");
            if (_resourceService.Exists(assetPath)) throw new Exception($"Cannot create asset because another file exists at {assetPath}");
			
            var data = _serializer.Serialize(asset);
            if (!await _resourceService.Write(data, assetPath)) throw new Exception("Asset creation failed");

            var meta = new AssetMeta(id);
            await _metaFilesService.CreateMetaFile(meta, assetPath);
			
            _assetRegistry.AddToLoadedAssets(new AssetInfo(meta, assetPath), asset);
			
            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
		
        return false;
    }
}
