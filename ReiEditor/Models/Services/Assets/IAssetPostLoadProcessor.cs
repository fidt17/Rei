namespace ReiEditor.Models.Services.Assets;

public interface IAssetPostLoadProcessor
{
    void Process(Asset asset);
}
