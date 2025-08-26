using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
    delegate void CallbackDelegate(IntPtr ptr);
	
    bool IsEngineRunning { get; }

    IntPtr CreateEngine(string resourcesDir, EngineRunMode mode);
    void Start(IntPtr enginePtr);
    void Shutdown(IntPtr enginePtr, int exitCode);
	
    void AddLogCallback(IntPtr ptr);
    void AddShutdownCallback(IntPtr callback);
	
    long BuildAsset(string assetPath, string destinationFile, long offset);

    Task<IntPtr> CreateEngineWindow();
    IntPtr GetWindowHandle(IntPtr windowPtr);
    void ResizeWindow(IntPtr windowPtr, int width, int height);
    void ChangeRenderMode(RenderMode mode);
    
    void SetDllPtr(IntPtr ptr);
    void Invoke(Type delegateType, string methodName = "", params object?[]? args);
    T Invoke<T>(Type delegateType, string methodName, params object?[]? args);
    Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args);
}