using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DataAssetSchemaService : IDataAssetSchemaService, IAssetPostLoadProcessor
{
    private readonly IDataAssetTypeRegistry _dataAssetTypeRegistry;
    private readonly ISerializedPropertiesService _serializedPropertiesService;
    private readonly ILogger<DataAssetSchemaService> _logger;

    public DataAssetSchemaService(IDataAssetTypeRegistry dataAssetTypeRegistry, ISerializedPropertiesService serializedPropertiesService, ILogger<DataAssetSchemaService> logger)
    {
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
        _serializedPropertiesService = serializedPropertiesService;
        _logger = logger;
    }

    public bool TryRefresh(DataAsset asset)
    {
        var dataAssetType = _dataAssetTypeRegistry.GetDataAssetType(asset.DataAssetTypeId);
        if (dataAssetType == null)
        {
            _logger.LogError($"Cannot refresh DataAsset {asset.AssetId}. Unknown type id {asset.DataAssetTypeId}");
            return false;
        }

        var objectInfo = dataAssetType.SerializableObject;
        foreach (var propertyName in asset.Properties.Keys.ToArray())
        {
            if (!objectInfo.SerializedProperties.ContainsKey(propertyName))
            {
                asset.RemoveProperty(propertyName);
            }
        }

        foreach (var propertyData in objectInfo.SerializedProperties)
        {
            if (!asset.HasProperty(propertyData.Key))
            {
                asset.AddProperty(_serializedPropertiesService.Create(propertyData.Key, propertyData.Value, null));
                continue;
            }

            var property = asset.GetProperty(propertyData.Key);
            if (property.Type != propertyData.Value.Type || property.SourceType != propertyData.Value.SourceType)
            {
                asset.RemoveProperty(propertyData.Key);
                asset.AddProperty(_serializedPropertiesService.Create(propertyData.Key, propertyData.Value, null));
                continue;
            }

            property.SetTemplateTypeName(propertyData.Value.TemplateTypeName);
            _serializedPropertiesService.Refresh(property);
        }

        return true;
    }

    public void Process(Asset asset)
    {
        if (asset is DataAsset dataAsset) TryRefresh(dataAsset);
    }
}
