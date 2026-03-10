using System;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Build.Assets;

public sealed class AssetBuildEngineSession : IDisposable
{
    public IEngineApi EngineApi { get; }

    private readonly Action? _disposeAction;

    public AssetBuildEngineSession(IEngineApi engineApi, Action? disposeAction = null)
    {
        EngineApi = engineApi;
        _disposeAction = disposeAction;
    }

    public void Dispose()
    {
        _disposeAction?.Invoke();
    }
}
