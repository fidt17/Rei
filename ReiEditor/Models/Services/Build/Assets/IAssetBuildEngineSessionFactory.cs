using System;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Build.Assets;

public interface IAssetBuildEngineSessionFactory
{
    AssetBuildEngineSession CreateSharedSession();
    AssetBuildEngineSession CreateIsolatedSession(string clientDllPath);
}
