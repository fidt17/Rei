using System.Text.Json.Serialization;

namespace ReiEditor.Models.Services.Assets;

public abstract class Asset
{
    [JsonIgnore]
    public AssetInfo AssetInfo { get; private set; }

    public void SetAssetInfo(AssetInfo assetInfo) => AssetInfo = assetInfo;
}