using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Logging.Engine;

public class EngineLogger : IEngineLogger
{
    private readonly IEngineApi.IntPtrCallbackDelegate _logCallbackDelegate;

    private readonly IEditorConsoleService _editorConsoleService;
    private readonly IEngineApi _engineApi;
    private readonly ConcurrentQueue<LogMessage> _messagesToLog = new();
    private readonly CancellationTokenSource _logPumpCancellationTokenSource = new();

    private const int MAX_LOG_BATCH_SIZE = 5;
    private static readonly TimeSpan LogInterval = TimeSpan.FromMilliseconds(100);

    public EngineLogger(IEditorConsoleService editorConsoleService, IEngineApi engineApi)
    {
        _editorConsoleService = editorConsoleService;
        _engineApi = engineApi;
        _logCallbackDelegate = HandleClientLogEvent;
    }

    public void SubscribeToClient()
    {
        _engineApi.AddLogCallback(Marshal.GetFunctionPointerForDelegate(_logCallbackDelegate));
        _ = Task.Run(LogPumpAsync);
    }

    private void HandleClientLogEvent(IntPtr messagePtr)
    {
        var messageStruct = Marshal.PtrToStructure<EngineLogMessage>(messagePtr);
        var logMessage = new LogMessage(LogScopeEnum.Engine, messageStruct.Level, DateTime.Now, messageStruct.Message, $"{messageStruct.Scope}\n{messageStruct.Details}");

        _messagesToLog.Enqueue(logMessage);
    }

    private async Task LogPumpAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(LogInterval);

            while (await timer.WaitForNextTickAsync(_logPumpCancellationTokenSource.Token))
            {
                DrainLogBatch();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DrainLogBatch()
    {
        for (var i = 0; i < MAX_LOG_BATCH_SIZE; i++)
        {
            if (!_messagesToLog.TryDequeue(out var message)) break;

            _editorConsoleService.Log(message);
        }
    }
}
