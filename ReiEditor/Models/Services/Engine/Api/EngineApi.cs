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
    private bool _isEngineRunning;
    
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
    public void Start(IntPtr enginePtr)
    {
        try
        {
            _isEngineRunning = true;
            Invoke(typeof(StartEngineDelegate), "Start", enginePtr);
        }
        catch (Exception e)
        {
            _isEngineRunning = false;
            throw;
        }
    }

    private delegate int ShutdownEngineDelegate(IntPtr enginePtr, int exitCode);
    public void Shutdown(IntPtr enginePtr, int exitCode)
    {
        _isEngineRunning = false;
        Invoke<int>(typeof(ShutdownEngineDelegate), "Shutdown", enginePtr, exitCode);
    }

    public void AddLogCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddLogCallback", callback);

    public void AddShutdownCallback(IntPtr callback) => Invoke(typeof(IEngineApi.CallbackDelegate), "AddShutdownCallback", callback);
    
    private delegate void GetEntityDataDelegate(int sceneEntityId, StringBuilder outputBuffer, int bufferSize);
    public GetEntityDataResponse? GetEntityData(int sceneEntityId)
    {
        try
        {
            if (!_isEngineRunning) return null;
            
            var outputBuffer = new StringBuilder(1024);
            Invoke(typeof(GetEntityDataDelegate), "GetEntityData", sceneEntityId, outputBuffer, outputBuffer.Capacity);

            return JsonConvert.DeserializeObject<GetEntityDataResponse>(outputBuffer.ToString());
        }
        catch (Exception e)
        {
            return null;
        }
    }

    private delegate void RenameEntityDelegate(int sceneEntityId, string newName);
    public bool RenameEntity(int sceneEntityId, string newName)
    {
        try
        {
            Invoke(typeof(RenameEntityDelegate), "RenameEntity", sceneEntityId, newName);
            return true;
        }
        catch (Exception)
        {
            // ignore
        }

        return false;
    }
    
    private delegate void SetEntityDataDelegate(string json);
    public bool SetEntityData(SetEntityDataRequest request)
    {
        try
        {
            Invoke(typeof(SetEntityDataDelegate), "SetEntityData", JsonConvert.SerializeObject(request));
            return true;
        }
        catch (Exception)
        {
            // ignore
        }

        return false;
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