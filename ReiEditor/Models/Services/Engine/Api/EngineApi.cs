using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Api;

public class EngineApi : IEngineApi
{
    private IntPtr _dllPtr;
    private readonly ILogger<EngineApi> _logger;

    public EngineApi(ILogger<EngineApi> logger)
    {
        _logger = logger;
    }

    public void SetDllPtr(IntPtr dllPtr)
    {
        if (dllPtr == IntPtr.Zero) throw new Exception("Invalid dll");
        _dllPtr = dllPtr;
    }

    private delegate IntPtr CreateEngineDelegate();
    public IntPtr CreateEngine() => Invoke<IntPtr>(typeof(CreateEngineDelegate), "CreateEngine");

    private delegate void StartEngineDelegate(IntPtr enginePtr);
    public void Start(IntPtr enginePtr) => Invoke(typeof(StartEngineDelegate), "Start", enginePtr);
	
    private delegate int ShutdownEngineDelegate(IntPtr enginePtr, int exitCode);
    public int Shutdown(IntPtr enginePtr, int exitCode) => Invoke<int>(typeof(ShutdownEngineDelegate), "Shutdown", enginePtr, exitCode);

    public void AddLogCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddLogCallback", callback);

    private delegate long BuildAssetDelegate(string path, string dest, long offset);

    public void AddShutdownCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddShutdownCallback", callback);

    public long BuildAsset(string assetPath, string destinationFile, long offset) => Invoke<long>(typeof(BuildAssetDelegate), "BuildAsset", assetPath, destinationFile, offset);

    private delegate IntPtr CreatePlaymodeWindowDelegate();
    public Task<IntPtr> CreatePlaymodeWindow() => InvokeAsync<IntPtr>(typeof(CreatePlaymodeWindowDelegate), "CreatePlaymodeWindow");

    #region UTILS
	
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
	
    private void Invoke(Type delegateType, [CallerMemberName] string methodName = "", params object?[]? args)
    {
        try
        {
            _logger.Log($"Invoke [{methodName}]");
            var d = Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, methodName), delegateType);
            d.DynamicInvoke(args);
        }
        catch (Exception)
        {
            _logger.LogError($"Dll invoke error [{methodName}]");
            throw;
        }
    }

    private T Invoke<T>(Type delegateType, string methodName, params object?[]? args)
    {
        try
        {
            _logger.Log($"Invoke [{methodName}]");
            return (T?) Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, methodName), delegateType).DynamicInvoke(args) ?? throw new InvalidOperationException(methodName);
        }
        catch (Exception)
        {
            _logger.LogError($"Dll invoke error [{methodName}]");
            throw;
        }
    }

    private Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args)
    {
        return Task.Run(() => Invoke<T>(delegateType, methodName, args));
    }

    #endregion
}