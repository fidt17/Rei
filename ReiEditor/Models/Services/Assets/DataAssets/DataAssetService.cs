using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DataAssetService : IDataAssetService
{
    private readonly IAssetCreator _assetCreator;
    private readonly IAssetsService _assetsService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IDataAssetSchemaService _dataAssetSchemaService;
    private readonly ISerializedPropertiesService _serializedPropertiesService;
    private readonly IDataAssetTypeRegistry _dataAssetTypeRegistry;
    private readonly IAssetRuntimeSyncService _assetRuntimeSyncService;
    private readonly ILogger<DataAssetService> _logger;

    public DataAssetService(IAssetCreator assetCreator, IAssetsService assetsService, IAssetRegistry assetRegistry, IDataAssetSchemaService dataAssetSchemaService, ISerializedPropertiesService serializedPropertiesService, IDataAssetTypeRegistry dataAssetTypeRegistry, IAssetRuntimeSyncService assetRuntimeSyncService, ILogger<DataAssetService> logger)
    {
        _assetCreator = assetCreator;
        _assetsService = assetsService;
        _assetRegistry = assetRegistry;
        _dataAssetSchemaService = dataAssetSchemaService;
        _serializedPropertiesService = serializedPropertiesService;
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
        _assetRuntimeSyncService = assetRuntimeSyncService;
        _logger = logger;
    }

    public async Task<DataAsset?> Create(int dataAssetTypeId, string projectPath)
    {
        var dataAssetType = _dataAssetTypeRegistry.GetDataAssetType(dataAssetTypeId);
        if (dataAssetType == null)
        {
            _logger.LogError($"Cannot create DataAsset. Unknown type id {dataAssetTypeId}");
            return null;
        }

        if (!Path.GetExtension(projectPath).Equals(FileExtensions.ASSET, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError($"Cannot create DataAsset. Path must end with {FileExtensions.ASSET}: {projectPath}");
            return null;
        }

        var asset = new DataAsset(dataAssetTypeId);
        if (!_dataAssetSchemaService.TryRefresh(asset)) return null;

        if (!await _assetCreator.Create(asset, projectPath)) return null;
        return asset;
    }

    public async Task<DataAsset?> Load(string assetId)
    {
        if (!_assetRegistry.TryGetById(assetId, out var assetInfo) || assetInfo is not DataAssetInfo) return null;

        var asset = await _assetsService.Load<DataAsset>(assetId);
        if (asset == null) return null;
        if (_dataAssetTypeRegistry.GetDataAssetType(asset.DataAssetTypeId) != null) return asset;

        _assetsService.Unload(assetId);
        return null;
    }

    public bool TryGetDataAssetTypeId(string assetId, out int dataAssetTypeId)
    {
        dataAssetTypeId = -1;
        if (!_assetRegistry.TryGetById(assetId, out var assetInfo) || assetInfo is not DataAssetInfo dataAssetInfo) return false;

        dataAssetTypeId = dataAssetInfo.DataAssetTypeId;
        return _dataAssetTypeRegistry.GetDataAssetType(dataAssetTypeId) != null;
    }

    public bool TrySetProperty(DataAsset asset, string propertyName, object? value, out bool runtimeSynced)
    {
        runtimeSynced = false;
        if (!asset.HasProperty(propertyName)) return false;

        _serializedPropertiesService.ApplyValue(asset.GetProperty(propertyName), value);
        runtimeSynced = TrySyncRuntime(asset);
        return true;
    }

    public bool TrySyncRuntime(DataAsset asset) => _assetRuntimeSyncService.TrySetAssetData(asset.AssetId, SerializeRuntimeData(asset));

    private static string SerializeRuntimeData(DataAsset asset) => SerializedPropertyJsonConverter.SerializeRuntimeProperties(asset.Properties.Values).ToString(Formatting.None);
}
