using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Api;

public class EngineApi : IEngineApi
{
    private delegate IntPtr ActionDelegate();
    
    private IntPtr _dllPtr;
    private readonly bool _logInvokingMethods;
    private readonly ILogger<EngineApi> _logger;

    public EngineApi(ILogger<EngineApi> logger)
    {
        _logger = logger;
        _logInvokingMethods = false;
    }

    public void SetDllPtr(IntPtr dllPtr)
    {
        if (dllPtr == IntPtr.Zero) throw new Exception("Invalid dll");
        _dllPtr = dllPtr;
    }

    private delegate IntPtr CreateEngineDelegate(string resourcesDir);
    public IntPtr CreateEngine(string resourcesDir)
    {
        var ptr = Invoke<IntPtr>(typeof(CreateEngineDelegate), "CreateEngine", resourcesDir);
        if (ptr == IntPtr.Zero) throw new Exception("Could not create engine");
        
        return ptr;
    }

    private delegate void StartEngineDelegate(IntPtr enginePtr);
    public void Start(IntPtr enginePtr) => Invoke(typeof(StartEngineDelegate), "Start", enginePtr);
	
    private delegate int ShutdownEngineDelegate(IntPtr enginePtr, int exitCode);
    public void Shutdown(IntPtr enginePtr, int exitCode) => Invoke<int>(typeof(ShutdownEngineDelegate), "Shutdown", enginePtr, exitCode);

    public void AddLogCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddLogCallback", callback);

    public void AddShutdownCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddShutdownCallback", callback);
    
    private delegate void GetSceneEntityStateDelegate(int sceneEntityId, StringBuilder outputBuffer, int bufferSize);
    public EntityStateResponse? GetSceneEntityState(int sceneEntityId)
    {
        try
        {
            var outputBuffer = new StringBuilder(1024);
            Invoke(typeof(GetSceneEntityStateDelegate), "GetSceneEntityState", sceneEntityId, outputBuffer, outputBuffer.Capacity);

            return JsonConvert.DeserializeObject<EntityStateResponse>(outputBuffer.ToString());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private delegate long BuildAssetDelegate(string path, string dest, long offset);
    public long BuildAsset(string assetPath, string destinationFile, long offset) => Invoke<long>(typeof(BuildAssetDelegate), "BuildAsset", assetPath, destinationFile, offset);

    public Task<IntPtr> CreatePlaymodeWindow() => InvokeAsync<IntPtr>(typeof(ActionDelegate), "CreatePlaymodeWindow");

    private delegate IntPtr GetWindowHandleDelegate(IntPtr windowPtr);
    public IntPtr GetWindowHandle(IntPtr windowPtr) => Invoke<IntPtr>(typeof(GetWindowHandleDelegate), "GetWindowHandle", windowPtr);

    private delegate IntPtr ResizeWindowDelegate(IntPtr windowPtr, int width, int height);
    public void ResizeWindow(IntPtr windowPtr, int width, int height) => Invoke(typeof(ResizeWindowDelegate), "ResizeWindow", windowPtr, width, height);

    #region UTILS
	
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
	
    private void Invoke(Type delegateType, [CallerMemberName] string methodName = "", params object?[]? args)
    {
        try
        {
            if (_logInvokingMethods)
            {
                _logger.Log($"Invoke [{methodName}]");
            }
            var d = Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, methodName), delegateType);
            d.DynamicInvoke(args);
        }
        catch (Exception e)
        {
            _logger.LogError($"Dll invoke error [{methodName}]. Exception: {e}");
            throw;
        }
    }

    private T Invoke<T>(Type delegateType, string methodName, params object?[]? args)
    {
        try
        {
            if (_logInvokingMethods)
            {
                _logger.Log($"Invoke [{methodName}]");
            }
            return (T?) Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr, methodName), delegateType).DynamicInvoke(args) ?? throw new InvalidOperationException(methodName);
        }
        catch (Exception e)
        {
            _logger.LogError($"Dll invoke error [{methodName}]. Exception: {e}");
            throw;
        }
    }

    private Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args)
    {
        return Task.Run(() => Invoke<T>(delegateType, methodName, args));
    }

    #endregion
}