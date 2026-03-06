namespace ReiEditor.Models.Services.Engine.Api;

public interface IAssetApi
{
    bool TryGetAssetData(string assetId, string assetType, out string jsonData);
    bool TrySetAssetData(string assetId, string assetType, string jsonData);
    bool TryPatchAssetData(string assetId, string assetType, string jsonPatch);
}
