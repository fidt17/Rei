using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;

namespace ReiEditor.Models.Services.Engine.Input;

public class EngineInputService : IEngineInputService, IDisposable
{
    public event Action<EngineEditorInputEvent>? InputReceivedEvent;

    private readonly IEngineApi.IntPtrCallbackDelegate _inputCallbackDelegate;
    private readonly IEngineApi _engineApi;
    private readonly ConcurrentQueue<EngineEditorInputEvent> _pendingInputs = new();
    private readonly CancellationTokenSource _pumpCancellationTokenSource = new();

    private bool _pumpStarted;

    private const int MAX_INPUT_BATCH_SIZE = 16;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(16);

    public EngineInputService(IEngineApi engineApi)
    {
        _engineApi = engineApi;
        _inputCallbackDelegate = HandleEditorInputEvent;
    }

    public void Dispose()
    {
        _pumpCancellationTokenSource.Cancel();
        _pumpCancellationTokenSource.Dispose();
    }

    public void SubscribeToClient()
    {
        _engineApi.AddEditorInputCallback(Marshal.GetFunctionPointerForDelegate(_inputCallbackDelegate));

        if (_pumpStarted) return;

        _pumpStarted = true;
        _ = Task.Run(PumpAsync);
    }

    private void HandleEditorInputEvent(IntPtr inputPtr)
    {
        var inputEvent = Marshal.PtrToStructure<EngineEditorInputEvent>(inputPtr);
        _pendingInputs.Enqueue(inputEvent);
    }

    private async Task PumpAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(PumpInterval);
            while (await timer.WaitForNextTickAsync(_pumpCancellationTokenSource.Token))
            {
                DrainBatch();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DrainBatch()
    {
        for (var i = 0; i < MAX_INPUT_BATCH_SIZE; i++)
        {
            if (!_pendingInputs.TryDequeue(out var inputEvent)) break;

            Dispatcher.UIThread.Post(() => InputReceivedEvent?.Invoke(inputEvent));
        }
    }
}
