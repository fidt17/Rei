using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies engine stop and DLL unload gating without loading native libraries.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class EngineBuildGateTests
{
    private sealed class TestClientDllManager : IClientDllManager
    {
        public Observable<bool> Loaded { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> DllLoaded => Loaded;
        public bool DllExists(string? dllPath = null) => throw new NotSupportedException();
        public void LoadDll(string? dllPath = null) => throw new NotSupportedException();
        public bool UnloadDll() => throw new NotSupportedException();
    }

    /// <summary>Engine stop runs even when client DLL is already unloaded.</summary>
    [Fact]
    public async Task UnloadedDllStillStopsEngine()
    {
        var runner = new TestEngineRunner();
        var stopCalls = 0;
        runner.OnStop = () => { stopCalls++; return Task.CompletedTask; };
        var dll = new TestClientDllManager();
        var service = new EngineBuildGate(runner, dll, new TestLogger<EngineBuildGate>());

        await service.StopEngineAndWaitForDllUnload(CancellationToken.None);

        Assert.Equal(1, stopCalls);
    }

    /// <summary>A DLL unloaded by controlled engine shutdown allows gate completion.</summary>
    [Fact]
    public async Task StopCanUnloadDllBeforePolling()
    {
        var runner = new TestEngineRunner();
        var dll = new TestClientDllManager();
        dll.Loaded.Value = true;
        runner.OnStop = () => { dll.Loaded.Value = false; return Task.CompletedTask; };
        var service = new EngineBuildGate(runner, dll, new TestLogger<EngineBuildGate>());

        await service.StopEngineAndWaitForDllUnload(CancellationToken.None);

        Assert.False(dll.Loaded.Value);
    }

    /// <summary>An engine stop exception propagates before DLL polling or gate delay.</summary>
    [Fact]
    public async Task StopFailurePropagates()
    {
        var failure = new IOException("controlled stop failure");
        var runner = new TestEngineRunner { OnStop = () => Task.FromException(failure) };
        var dll = new TestClientDllManager();
        dll.Loaded.Value = true;
        var service = new EngineBuildGate(runner, dll, new TestLogger<EngineBuildGate>());

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.StopEngineAndWaitForDllUnload(CancellationToken.None)));
    }

    /// <summary>Cancellation requested by engine shutdown aborts immediately after stop completes.</summary>
    [Fact]
    public async Task CancellationAfterStopAbortsBeforePolling()
    {
        var stopCalls = 0;
        using var cancellation = new CancellationTokenSource();
        var runner = new TestEngineRunner
        {
            OnStop = () =>
            {
                stopCalls++;
                cancellation.Cancel();
                return Task.CompletedTask;
            },
        };
        var dll = new TestClientDllManager();
        dll.Loaded.Value = true;
        var service = new EngineBuildGate(runner, dll, new TestLogger<EngineBuildGate>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StopEngineAndWaitForDllUnload(cancellation.Token));

        Assert.Equal(1, stopCalls);
        Assert.True(dll.Loaded.Value);
    }

    /// <summary>Cancellation interrupts polling while DLL remains loaded.</summary>
    [Fact]
    public async Task CancellationInterruptsDllPolling()
    {
        var runner = new TestEngineRunner { OnStop = () => Task.CompletedTask };
        var dll = new TestClientDllManager();
        dll.Loaded.Value = true;
        var service = new EngineBuildGate(runner, dll, new TestLogger<EngineBuildGate>());
        using var cancellation = new CancellationTokenSource();

        var task = service.StopEngineAndWaitForDllUnload(cancellation.Token);
        Assert.False(task.IsCompleted);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        Assert.True(dll.Loaded.Value);
    }
}
