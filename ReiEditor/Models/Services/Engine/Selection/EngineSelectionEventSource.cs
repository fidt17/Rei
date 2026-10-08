using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Engine.Selection;

public sealed class EngineSelectionEventSource : IEngineSelectionEventSource
{
    private delegate void SetCallbackDelegate(IntPtr callback);

    public event Action<int[]>? SelectionReceived;
    public bool IsEventSyncEnabled => _callback != null;

    private readonly IEngineApi _api;
    private readonly ILogger<EngineSelectionEventSource> _logger;
    private IEngineApi.SelectionCallbackDelegate? _callback;

    public EngineSelectionEventSource(IEngineApi api, ILogger<EngineSelectionEventSource> logger)
    {
        _api = api;
        _logger = logger;
    }

    public void Connect()
    {
        _callback = Receive;
        _api.Invoke(typeof(SetCallbackDelegate), "SetEditorSelectionCallback", Marshal.GetFunctionPointerForDelegate(_callback));
    }

    public void Disconnect()
    {
        if (_callback == null) return;
        // EngineRunner calls this after the engine loop exits, before DLL unload.
        _api.Invoke(typeof(SetCallbackDelegate), "SetEditorSelectionCallback", IntPtr.Zero);
        _callback = null;
    }

    private void Receive(IntPtr json, int byteCount)
    {
        try
        {
            var ids = JsonConvert.DeserializeObject<int[]>(Marshal.PtrToStringUTF8(json, byteCount) ?? "[]");
            if (ids != null) SelectionReceived?.Invoke(ids);
        }
        // Managed exceptions must not escape through the native callback.
        catch (Exception error) { _logger.LogException(error); }
    }
}
