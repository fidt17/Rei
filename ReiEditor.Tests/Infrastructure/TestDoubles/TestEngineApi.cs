using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.TransformationControls;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Rejects all engine operations by default; focused subclasses override only explicitly supported managed responses.</summary>
internal abstract class TestEngineApi : IEngineApi
{
    public virtual bool IsEngineRunning => throw new NotSupportedException();
    public virtual IntPtr CreateEngine(string resourcesDir, EngineRunMode mode) => throw new NotSupportedException();
    public virtual void Start(IntPtr enginePtr) => throw new NotSupportedException();
    public virtual void Shutdown(IntPtr enginePtr, int exitCode) => throw new NotSupportedException();
    public virtual void DestroyEngine(IntPtr enginePtr) => throw new NotSupportedException();
    public virtual void AddLogCallback(IntPtr ptr) => throw new NotSupportedException();
    public virtual void AddEngineStartCallback(IntPtr callback) => throw new NotSupportedException();
    public virtual void AddShutdownCallback(IntPtr callback) => throw new NotSupportedException();
    public virtual void AddEditorInputCallback(IntPtr callback) => throw new NotSupportedException();
    public virtual long BuildAsset(string assetPath, string destinationFile, long offset) => throw new NotSupportedException();
    public virtual Task<IntPtr> CreateEngineWindow() => throw new NotSupportedException();
    public virtual IntPtr GetWindowHandle(IntPtr windowPtr) => throw new NotSupportedException();
    public virtual void ResizeWindow(IntPtr windowPtr, int width, int height) => throw new NotSupportedException();
    public virtual void ChangeRenderMode(RenderMode mode, bool isUiRenderingEnabled) => throw new NotSupportedException();
    public virtual void SetEditorGridSettings(SetViewportGridSettingsRequest settings) => throw new NotSupportedException();
    public virtual void ChangeTransformationMode(TransformationMode mode, bool worldSpace) => throw new NotSupportedException();
    public virtual int GetTransformationMode() => throw new NotSupportedException();
    public virtual bool RequestFrameCapture(IntPtr callback) => throw new NotSupportedException();
    public virtual void MarkEngineStopped() => throw new NotSupportedException();
    public virtual void SetDllPtr(IntPtr ptr) => throw new NotSupportedException();
    public virtual void Invoke(Type delegateType, string methodName = "", params object?[]? args) => throw new NotSupportedException();
    public virtual T Invoke<T>(Type delegateType, string methodName, params object?[]? args) => throw new NotSupportedException();
    public virtual Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args) => throw new NotSupportedException();
}
