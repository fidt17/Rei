using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.TransformationControls;

namespace ReiEditor.Models.Services.Engine.Api;

public class EngineApi : IEngineApi
{
    private delegate IntPtr ActionDelegate();
    
    public bool IsEngineRunning { get; private set; }
    
    private IntPtr? _dllPtr;

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

    private delegate IntPtr CreateEngineDelegate(string resourcesDir, int mode);
    public IntPtr CreateEngine(string resourcesDir, EngineRunMode mode)
    {
        var ptr = Invoke<IntPtr>(typeof(CreateEngineDelegate), "CreateEngine", resourcesDir, mode);
        if (ptr == IntPtr.Zero) throw new Exception("Could not create engine");
        
        return ptr;
    }

    private delegate void StartEngineDelegate(IntPtr enginePtr);
    public void Start(IntPtr enginePtr)
    {
        IsEngineRunning = true;
        try
        {
            Invoke(typeof(StartEngineDelegate), "Start", enginePtr);
        }
        finally
        {
            IsEngineRunning = false;
        }
    }

    private delegate int ShutdownEngineDelegate(IntPtr enginePtr, int exitCode);
    public void Shutdown(IntPtr enginePtr, int exitCode)
    {
        IsEngineRunning = false;
        Invoke<int>(typeof(ShutdownEngineDelegate), "Shutdown", enginePtr, exitCode);
    }

    private delegate void DestroyEngineDelegate(IntPtr enginePtr);
    public void DestroyEngine(IntPtr enginePtr)
    {
        IsEngineRunning = false;
        Invoke(typeof(DestroyEngineDelegate), "DestroyEngine", enginePtr);
    }

    public void AddLogCallback(IntPtr callback) => Invoke(typeof(IEngineApi.FunctionPointerDelegate), "AddLogCallback", callback);
    
    public void AddEngineStartCallback(IntPtr callback) => Invoke(typeof(IEngineApi.FunctionPointerDelegate), "AddEngineStartCallback", callback);

    public void AddShutdownCallback(IntPtr callback) => Invoke(typeof(IEngineApi.FunctionPointerDelegate), "AddShutdownCallback", callback);

    public void AddEditorInputCallback(IntPtr callback) => Invoke(typeof(IEngineApi.FunctionPointerDelegate), "AddEditorInputCallback", callback);

    private delegate long BuildAssetDelegate(string path, string dest, long offset);
    public long BuildAsset(string assetPath, string destinationFile, long offset) => Invoke<long>(typeof(BuildAssetDelegate), "BuildAsset", assetPath, destinationFile, offset);

    public Task<IntPtr> CreateEngineWindow() => InvokeAsync<IntPtr>(typeof(ActionDelegate), "CreateEngineWindow");

    private delegate void ChangeRenderModeDelegate(int mode, bool isUiRenderingEnabled);
    public void ChangeRenderMode(RenderMode mode, bool isUiRenderingEnabled)
    {
        if (!IsEngineRunning) return;
        Invoke(typeof(ChangeRenderModeDelegate), "ChangeRenderMode", (int) mode, isUiRenderingEnabled);
    }
    
    private delegate void SetEditorGridSettingsDelegate(IntPtr settings);
    public void SetEditorGridSettings(SetViewportGridSettingsRequest settings)
    {
        if (!IsEngineRunning) return;

        try
        {
            int size = Marshal.SizeOf(typeof(SetViewportGridSettingsRequest));
            IntPtr ptr = Marshal.AllocHGlobal(size);
        
            try
            {
                Marshal.StructureToPtr(settings, ptr, false);
                Invoke(typeof(SetEditorGridSettingsDelegate), "SetEditorGridSettings", ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            } 
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
    
    private delegate void SetTransformationModeDelegate(int mode, bool worldSpace);
    public void ChangeTransformationMode(TransformationMode mode, bool worldSpace)
    {
        if (!IsEngineRunning) return;
        Invoke(typeof(SetTransformationModeDelegate), "ChangeTransformationMode", (int) mode, worldSpace);
    }

    public void MarkEngineStopped()
    {
        IsEngineRunning = false;
    }

    private delegate IntPtr GetWindowHandleDelegate(IntPtr windowPtr);
    public IntPtr GetWindowHandle(IntPtr windowPtr) => Invoke<IntPtr>(typeof(GetWindowHandleDelegate), "GetWindowHandle", windowPtr);

    private delegate IntPtr ResizeWindowDelegate(IntPtr windowPtr, int width, int height);
    public void ResizeWindow(IntPtr windowPtr, int width, int height)
    {
        if (!IsEngineRunning) return;
        Invoke(typeof(ResizeWindowDelegate), "ResizeWindow", windowPtr, width, height);
    }

    #region UTILS
	
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
	
    public void Invoke(Type delegateType, [CallerMemberName] string methodName = "", params object?[]? args)
    {
        try
        {
            if (_dllPtr == null) throw new Exception("Missing dll pointer");
            
            if (_logInvokingMethods)
            {
                _logger.Log($"Invoke [{methodName}]");
            }
            var d = Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr.Value, methodName), delegateType);
            d.DynamicInvoke(args);
        }
        catch (Exception e)
        {
            _logger.LogError($"Dll invoke error [{methodName}]. Exception: {e}");
            throw;
        }
    }

    public T Invoke<T>(Type delegateType, string methodName, params object?[]? args)
    {
        try
        {
            if (_dllPtr == null) throw new Exception("Missing dll pointer");
            
            if (_logInvokingMethods)
            {
                _logger.Log($"Invoke [{methodName}]");
            }
            return (T?) Marshal.GetDelegateForFunctionPointer(GetProcAddress(_dllPtr.Value, methodName), delegateType).DynamicInvoke(args) ?? throw new InvalidOperationException(methodName);
        }
        catch (Exception e)
        {
            _logger.LogError($"Dll invoke error [{methodName}]. Exception: {e}");
            throw;
        }
    }

    public Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args)
    {
        return Task.Run(() => Invoke<T>(delegateType, methodName, args));
    }

    #endregion
}
