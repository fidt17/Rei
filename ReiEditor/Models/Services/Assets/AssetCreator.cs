using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Serialization;

namespace ReiEditor.Models.Services.Assets;

public class AssetCreator : IAssetCreator
{
    public event Action<AssetInfo, Asset>? AssetCreatedEvent;
    
    private readonly IResourceService _resourceService;
    private readonly ISerializer _serializer;
    private readonly ILogger<AssetCreator> _logger;

    public AssetCreator(IResourceService resourceService, ISerializer serializer, ILogger<AssetCreator> logger)
    {
        _resourceService = resourceService;
        _serializer = serializer;
        _logger = logger;
    }

    public string AllocateAssetId()
    {
        return new(Guid.NewGuid().ToString());
    }

    public Task<bool> Create(Asset asset, string projectPath)
    {
        return Create(asset, AllocateAssetId(), projectPath);
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
            await CreateMetaFile(meta, assetPath);
			
            AssetCreatedEvent?.Invoke(new AssetInfo(meta, assetPath), asset);
			
            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
		
        return false;
    }

    public async Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath)
    {
        var metaPath = assetPath + FileExtensions.META;
        var didCreate = await _resourceService.Write(_serializer.Serialize(meta), metaPath);
        if (!didCreate) throw new Exception("Asset Meta file creation failed");

        return new ObjectFile<AssetMeta>(meta, metaPath);
    }
}