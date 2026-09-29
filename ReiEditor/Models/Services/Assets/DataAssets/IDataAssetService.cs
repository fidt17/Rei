using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Assets.DataAssets;

public interface IDataAssetService
{
    Task<DataAsset?> Create(int dataAssetTypeId, string projectPath);
    Task<DataAsset?> Load(string assetId);
    bool TryGetDataAssetTypeId(string assetId, out int dataAssetTypeId);
    bool TrySetProperty(DataAsset asset, string propertyName, object? value, out bool runtimeSynced);
    bool TrySyncRuntime(DataAsset asset);
}
