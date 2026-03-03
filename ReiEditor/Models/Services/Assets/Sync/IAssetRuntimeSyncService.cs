namespace ReiEditor.Models.Services.Assets.Sync;

public interface IAssetRuntimeSyncService
{
    bool TryGetAssetData(string assetId, out string jsonData);
    bool TrySetAssetData(string assetId, string jsonData);
    bool TryPatchAssetData(string assetId, string jsonPatch);
}

