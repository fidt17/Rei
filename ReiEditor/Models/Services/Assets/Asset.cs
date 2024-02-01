using System.Text.Json.Serialization;

namespace ReiEditor.Models.Services.Assets;

public abstract class Asset
{
    [JsonIgnore]
    public string AssetId { get; private set; } = "";

    public void SetAssetInfo(AssetInfo assetInfo)
    {
        AssetId = assetInfo.Meta.AssetId;
    }
}