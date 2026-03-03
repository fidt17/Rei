using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
    delegate void VoidCallbackDelegate();
    delegate void FunctionPointerDelegate(IntPtr ptr);
    delegate void IntPtrCallbackDelegate(IntPtr ptr);
    delegate void IntCallbackDelegate(int value);
	
    bool IsEngineRunning { get; }

    IntPtr CreateEngine(string resourcesDir, EngineRunMode mode);
    void Start(IntPtr enginePtr);
    void Shutdown(IntPtr enginePtr, int exitCode);
    void DestroyEngine(IntPtr enginePtr);
	
    void AddLogCallback(IntPtr ptr);
    void AddEngineStartCallback(IntPtr callback);
    void AddShutdownCallback(IntPtr callback);
	
    long BuildAsset(string assetPath, string destinationFile, long offset);

    Task<IntPtr> CreateEngineWindow();
    IntPtr GetWindowHandle(IntPtr windowPtr);
    void ResizeWindow(IntPtr windowPtr, int width, int height);
    void ChangeRenderMode(RenderMode mode);
    void SetEditorGridSettings(SetViewportGridSettingsRequest settings);
    void ChangeTransformationMode(bool worldSpace);

    void MarkEngineStopped();
    
    void SetDllPtr(IntPtr ptr);
    void Invoke(Type delegateType, string methodName = "", params object?[]? args);
    T Invoke<T>(Type delegateType, string methodName, params object?[]? args);
    Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args);
}
