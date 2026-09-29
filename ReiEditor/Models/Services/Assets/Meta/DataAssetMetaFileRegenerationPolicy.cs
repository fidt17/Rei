using System;

namespace ReiEditor.Models.Services.Assets.Meta;

public sealed class DataAssetMetaFileRegenerationPolicy : IMetaFileRegenerationPolicy
{
    private readonly Func<int> _allocateDataAssetTypeId;

    public DataAssetMetaFileRegenerationPolicy(Func<int> allocateDataAssetTypeId)
    {
        _allocateDataAssetTypeId = allocateDataAssetTypeId;
    }

    public void Apply(AssetMeta meta, string assetPath)
    {
        if (meta.TryGetData(DataAssetMeta.Key, out DataAssetMeta? _))
        {
            meta.AddData(DataAssetMeta.Key, new DataAssetMeta(_allocateDataAssetTypeId()));
        }
    }
}
