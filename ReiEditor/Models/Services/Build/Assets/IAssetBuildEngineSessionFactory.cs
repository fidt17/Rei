namespace ReiEditor.Models.Services.Build.Assets;

public interface IAssetBuildEngineSessionFactory
{
    AssetBuildEngineSession CreateSharedSession();
    AssetBuildEngineSession CreateIsolatedSession(string clientDllPath);
}
