using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Assets.Meta;

public sealed class DataAssetMeta
{
    public static string Key => "DataAssetMeta";

    [JsonProperty("DataAssetTypeId")]
    public int DataAssetTypeId { get; }

    public DataAssetMeta(int dataAssetTypeId)
    {
        DataAssetTypeId = dataAssetTypeId;
    }
}
