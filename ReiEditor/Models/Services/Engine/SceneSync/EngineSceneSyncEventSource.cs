using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.SceneSync;

public sealed class EngineSceneSyncEventSource : IEngineSceneSyncEventSource
{
    public event Action<SceneSyncPacket>? Received;
    public event Action? ConnectionReset;
    public bool IsConnected => _callback != null;

    private readonly IEngineApi _api;
    private readonly ILogger<EngineSceneSyncEventSource> _logger;
    private IEngineApi.SceneSyncCallbackDelegate? _callback;

    public EngineSceneSyncEventSource(IEngineApi api, ILogger<EngineSceneSyncEventSource> logger)
    {
        _api = api;
        _logger = logger;
    }

    public void Connect()
    {
        ConnectionReset?.Invoke();
        _callback = Receive;
        _api.SetSceneSyncCallback(_callback);
    }

    public void Disconnect()
    {
        if (_callback == null) return;
        // Engine loop has exited; unregister before unloading its DLL.
        _api.SetSceneSyncCallback(null);
        _callback = null;
        ConnectionReset?.Invoke();
    }

    public void RequestSnapshot()
    {
        if (!IsConnected) return;
        _api.RequestSceneSync();
    }

    private void Receive(IntPtr json, int byteCount)
    {
        try
        {
            var packet = JsonConvert.DeserializeObject<SceneSyncPacket>(Marshal.PtrToStringUTF8(json, byteCount) ?? "{}");
            if (packet != null) Received?.Invoke(packet);
        }
        // Managed exceptions must not escape through the native callback.
        catch (Exception error) { _logger.LogException(error); }
    }
}
