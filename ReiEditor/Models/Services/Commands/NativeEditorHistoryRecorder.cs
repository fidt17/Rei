using System;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Commands;

public sealed class NativeEditorHistoryRecorder : IDisposable
{
    private readonly IEngineApi _api;
    private readonly IEngineNativeAccess _native;
    private readonly IEditorCommandService _commands;
    private readonly ILogger<NativeEditorHistoryRecorder> _logger;
    private bool _disposed;

    public NativeEditorHistoryRecorder(IEngineApi api, IEngineNativeAccess native, IEditorCommandService commands, ILogger<NativeEditorHistoryRecorder> logger)
    {
        _api = api;
        _native = native;
        _commands = commands;
        _logger = logger;
        _api.ActionRecorded += Record;
    }

    public void Dispose()
    {
        _disposed = true;
        _api.ActionRecorded -= Record;
    }

    private void Record(ulong id)
    {
        var entry = new NativeEditorHistoryEntry(id, _native);
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !entry.CanUndo) return;
            try { _commands.RecordApplied(entry); }
            catch (Exception error) { _logger.LogException(error); }
        });
    }
}
