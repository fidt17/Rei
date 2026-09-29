using ReiEditor.Models.Services.Assets.Meta;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public sealed class DataAssetInfo : AssetInfo
{
    public int DataAssetTypeId { get; }

    public DataAssetInfo(AssetMeta meta, string fullPath, int dataAssetTypeId) : base(meta, fullPath)
    {
        DataAssetTypeId = dataAssetTypeId;
    }

    public override AssetInfo WithPath(string fullPath) => new DataAssetInfo(Meta, fullPath, DataAssetTypeId);
}
