using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.TransformationControls;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
    delegate void VoidCallbackDelegate();
    delegate void FunctionPointerDelegate(IntPtr ptr);
    delegate void IntPtrCallbackDelegate(IntPtr ptr);
    delegate void IntCallbackDelegate(int value);
    delegate void SceneSyncCallbackDelegate(IntPtr json, int byteCount);
    delegate void FrameCaptureCallbackDelegate(IntPtr pixels, int width, int height);
	
    event Action<int>? SceneLoadProgressChanged;

    bool IsEngineRunning { get; }
    bool HasExport(string name) => false;

    IntPtr CreateEngine(string resourcesDir, EngineRunMode mode);
    void Start(IntPtr enginePtr, string? lastSceneId = null);
    void Shutdown(IntPtr enginePtr, int exitCode);
    void DestroyEngine(IntPtr enginePtr);
	
    void AddLogCallback(IntPtr ptr);
    void AddEngineStartCallback(IntPtr callback);
    void AddShutdownCallback(IntPtr callback);
    void AddEditorInputCallback(IntPtr callback);
    void SetSceneSyncCallback(SceneSyncCallbackDelegate? callback);
    void RequestSceneSync();
    bool LoadEditorScene(string requestJson);
	
    long BuildAsset(string assetPath, string destinationFile, long offset);

    Task<IntPtr> CreateEngineWindow();
    IntPtr GetWindowHandle(IntPtr windowPtr);
    void ResizeWindow(IntPtr windowPtr, int width, int height);
    void ChangeRenderMode(RenderMode mode, bool isUiRenderingEnabled);
    void SetEditorGridSettings(SetViewportGridSettingsRequest settings);
    void ChangeTransformationMode(TransformationMode mode, bool worldSpace);
    int GetTransformationMode();
    bool RequestFrameCapture(IntPtr callback);

    void MarkEngineStarted() { }
    void MarkEngineStopped();

    void SetDllPtr(IntPtr ptr);
    void Invoke(Type delegateType, string methodName = "", params object?[]? args);
    T Invoke<T>(Type delegateType, string methodName, params object?[]? args);
    Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args);
}
