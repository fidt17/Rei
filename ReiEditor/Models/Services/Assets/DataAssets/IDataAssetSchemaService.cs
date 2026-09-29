namespace ReiEditor.Models.Services.Assets.DataAssets;

public interface IDataAssetSchemaService
{
    bool TryRefresh(DataAsset asset);
}
