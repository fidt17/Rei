using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IEngineApi
{
    delegate void CallbackDelegate(IntPtr ptr);
	
    bool IsEngineRunning { get; }

    IntPtr CreateEngine(string resourcesDir);
    void Start(IntPtr enginePtr);
    void Shutdown(IntPtr enginePtr, int exitCode);
	
    void AddLogCallback(IntPtr ptr);
    void AddShutdownCallback(IntPtr callback);
	
    long BuildAsset(string assetPath, string destinationFile, long offset);

    Task<IntPtr> CreatePlaymodeWindow();
    IntPtr GetWindowHandle(IntPtr windowPtr);
    void ResizeWindow(IntPtr windowPtr, int width, int height);

    void SetDllPtr(IntPtr ptr);
    void Invoke(Type delegateType, [CallerMemberName] string methodName = "", params object?[]? args);
    T Invoke<T>(Type delegateType, string methodName, params object?[]? args);
    Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args);
}