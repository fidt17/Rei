using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Tests.Infrastructure.Headless;

namespace ReiEditor.Tests.Models.Services.Engine.Input;

/// <summary>Verifies native-shaped input values are copied and delivered in order on the UI dispatcher.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Engine")]
public sealed class EngineInputServiceTests
{
    private sealed class TestInputApi : Infrastructure.TestDoubles.TestEngineApi
    {
        public IEngineApi.IntPtrCallbackDelegate? Callback { get; private set; }
        public override void AddEditorInputCallback(IntPtr callback) => Callback = Marshal.GetDelegateForFunctionPointer<IEngineApi.IntPtrCallbackDelegate>(callback);
    }

    /// <summary>More than one input batch preserves copied payloads and ordering after the original unmanaged buffer is freed.</summary>
    [AvaloniaFact]
    public async Task CopiesAndDispatchesMultipleInputBatches()
    {
        var api = new TestInputApi();
        using var service = new EngineInputService(api);
        var received = new List<EngineEditorInputEvent>();
        var onUiThread = new List<bool>();
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.InputReceivedEvent += input =>
        {
            received.Add(input);
            onUiThread.Add(Dispatcher.UIThread.CheckAccess());
            if (received.Count == 34) complete.TrySetResult();
        };
        service.SubscribeToClient();
        Assert.NotNull(api.Callback);
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<EngineEditorInputEvent>());
        try
        {
            for (var index = 0; index < 34; index++)
            {
                var input = new EngineEditorInputEvent { Type = EngineEditorInputEventType.KeyDown, Code = index, Mods = 2, MouseX = index + 0.5f, MouseY = -index };
                Marshal.StructureToPtr(input, buffer, false);
                api.Callback(buffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        await complete.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Enumerable.Range(0, 34), received.Select(input => input.Code));
        Assert.All(onUiThread, Assert.True);
        for (var index = 0; index < received.Count; index++)
        {
            Assert.Equal(EngineEditorInputEventType.KeyDown, received[index].Type);
            Assert.Equal(2, received[index].Mods);
            Assert.Equal(index + 0.5f, received[index].MouseX);
            Assert.Equal(-index, received[index].MouseY);
        }
    }
}
