using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Tests.Models.Services.Engine.Playmode;

/// <summary>Verifies managed shutdown callback registration, engine state reset and exit-code delivery.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Engine")]
public sealed class EngineShutdownListenerTests
{
    private sealed class TestShutdownApi : Infrastructure.TestDoubles.TestEngineApi
    {
        public IEngineApi.IntCallbackDelegate? Callback { get; private set; }
        public int Stops { get; private set; }
        public override void AddShutdownCallback(IntPtr callback) => Callback = Marshal.GetDelegateForFunctionPointer<IEngineApi.IntCallbackDelegate>(callback);
        public override void MarkEngineStopped() => Stops++;
    }

    /// <summary>The callback marks engine stopped before asynchronously publishing its exact exit code.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task CallbackResetsEngineBeforePublishingExitCode(int exitCode)
    {
        var api = new TestShutdownApi();
        var listener = new EngineShutdownListener(api);
        var received = new TaskCompletionSource<(int Code, int Stops)>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.EngineShutdownEvent += code => received.TrySetResult((code, api.Stops));
        listener.SubscribeToClient();

        Assert.NotNull(api.Callback);
        api.Callback(exitCode);
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal((exitCode, 1), result);
        GC.KeepAlive(listener);
    }
}
