using System;
using System.IO;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Build.Assets;

public class AssetBuildEngineSessionFactory : IAssetBuildEngineSessionFactory
{
    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    private readonly IClientDllManager _dllManager;
    private readonly IEngineApi _engineApi;
    private readonly IEngineLogger _engineLogger;
    private readonly ILogger<EngineApi> _engineApiLogger;

    public AssetBuildEngineSessionFactory(
        IClientDllManager dllManager,
        IEngineApi engineApi,
        IEngineLogger engineLogger,
        ILogger<EngineApi> engineApiLogger)
    {
        _dllManager = dllManager;
        _engineApi = engineApi;
        _engineLogger = engineLogger;
        _engineApiLogger = engineApiLogger;
    }

    public AssetBuildEngineSession CreateSharedSession()
    {
        Action? disposeAction = null;
        if (!_dllManager.DllLoaded.Value)
        {
            _dllManager.LoadDll();
            disposeAction = () => _dllManager.UnloadDll();
        }

        _engineLogger.SubscribeToClient();
        return new AssetBuildEngineSession(_engineApi, disposeAction);
    }

    public AssetBuildEngineSession CreateIsolatedSession(string clientDllPath)
    {
        var dllHandle = LoadClientDll(clientDllPath);
        var engineApi = new EngineApi(_engineApiLogger);
        engineApi.SetDllPtr(dllHandle);
        return new AssetBuildEngineSession(engineApi, () => FreeLibrary(dllHandle));
    }

    private static IntPtr LoadClientDll(string clientDllPath)
    {
        var fullDllPath = Path.GetFullPath(clientDllPath);
        if (!File.Exists(fullDllPath)) throw new FileNotFoundException("Client dll for asset build is missing.", fullDllPath);

        var dllHandle = LoadLibraryEx(fullDllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (dllHandle == IntPtr.Zero)
            throw new Exception($"Could not load client dll for asset build: {fullDllPath}. Win32Error={Marshal.GetLastWin32Error()}");

        return dllHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr fileHandle, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr moduleHandle);
}
