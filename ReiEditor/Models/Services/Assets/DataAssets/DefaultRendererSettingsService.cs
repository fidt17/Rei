using System;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DefaultRendererSettingsService : IDefaultRendererSettingsService
{
    private const string DIRECTORY = "Settings/Rendering";
    private const string NAME = "Renderer Settings";
    private const string TYPE = "RendererSettings";

    private readonly IAssetRegistry _assetRegistry;
    private readonly IDataAssetTypeRegistry _types;
    private readonly IDataAssetSchemaService _schema;
    private readonly IAssetsService _assets;
    private readonly IAssetCreator _creator;
    private readonly IResourceService _resources;

    public DefaultRendererSettingsService(IAssetRegistry assetRegistry, IDataAssetTypeRegistry types, IDataAssetSchemaService schema, IAssetsService assets, IAssetCreator creator, IResourceService resources)
    {
        _assetRegistry = assetRegistry;
        _types = types;
        _schema = schema;
        _assets = assets;
        _creator = creator;
        _resources = resources;
    }

    public async Task EnsureCreated()
    {
        var type = _types.GetDataAssetType(TYPE) ?? throw new InvalidOperationException("RendererSettings type is not registered.");
        if (_assetRegistry.TryGetById(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS, out var info))
        {
            if (info is not DataAssetInfo dataInfo || dataInfo.DataAssetTypeId != type.TypeId)
                throw new InvalidOperationException("Default RendererSettings ID belongs to a different asset type.");
            if (await _assets.Load<DataAsset>(SpecialAssetIds.DEFAULT_RENDERER_SETTINGS) == null)
                throw new InvalidOperationException("Default RendererSettings could not be loaded.");
            return;
        }

        var asset = new DataAsset(type.TypeId);
        if (!_schema.TryRefresh(asset)) throw new InvalidOperationException("Default RendererSettings schema could not be created.");
        var name = PathNamingUtils.GetUniqueAssetName(_resources.GetProjectPath(DIRECTORY), NAME, FileExtensions.ASSET);
        if (!await _creator.Create(asset, SpecialAssetIds.DEFAULT_RENDERER_SETTINGS, $"{DIRECTORY}/{name}{FileExtensions.ASSET}"))
            throw new InvalidOperationException("Default RendererSettings creation failed.");
    }
}
