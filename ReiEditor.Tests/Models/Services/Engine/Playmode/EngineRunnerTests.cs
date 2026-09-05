using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Engine;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Engine.Playmode;

/// <summary>Verifies engine runner lifecycle with managed callbacks, controlled worker completion and no native engine.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Engine")]
public sealed class EngineRunnerTests
{
    private sealed class TestRunnerApi(TestContext context) : Infrastructure.TestDoubles.TestEngineApi
    {
        private IEngineApi.VoidCallbackDelegate? _started;
        public bool Running { get; private set; }
        public override bool IsEngineRunning => Running;
        public (string Resources, EngineRunMode Mode)? Creation { get; private set; }
        public override IntPtr CreateEngine(string resourcesDir, EngineRunMode mode)
        {
            context.Touch("create");
            Creation = (resourcesDir, mode);
            return new IntPtr(123);
        }
        public override void AddEngineStartCallback(IntPtr callback)
        {
            context.Touch("callback");
            _started = Marshal.GetDelegateForFunctionPointer<IEngineApi.VoidCallbackDelegate>(callback);
        }
        public override void Start(IntPtr enginePtr)
        {
            Assert.Equal(new IntPtr(123), enginePtr);
            context.Touch("start");
            Running = true;
            _started!();
            context.Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        public override void Shutdown(IntPtr enginePtr, int exitCode)
        {
            Assert.Equal(new IntPtr(123), enginePtr);
            Assert.Equal(1, exitCode);
            context.Touch("shutdown");
            Running = false;
            context.Shutdown.Publish(exitCode);
            context.Release.TrySetResult();
        }
        public override void DestroyEngine(IntPtr enginePtr)
        {
            Assert.Equal(new IntPtr(123), enginePtr);
            context.Touch("destroy");
        }
        public override void MarkEngineStopped() { Running = false; context.Calls.Enqueue("stopped"); }
    }

    private sealed class TestDllManager(TestContext context) : IClientDllManager
    {
        public Observable<bool> Loaded { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> DllLoaded => Loaded;
        public bool DllExists(string? dllPath = null) => throw new NotSupportedException();
        public void LoadDll(string? dllPath = null)
        {
            context.Touch("load");
            Loaded.Value = true;
            context.LoadSucceeded = true;
        }
        public bool UnloadDll()
        {
            context.Calls.Enqueue("unload");
            Loaded.Value = false;
            context.Unloaded.TrySetResult();
            return true;
        }
    }

    private sealed class TestShutdownListener(TestContext context) : IEngineShutdownListener
    {
        public event Action<int>? EngineShutdownEvent;
        public void SubscribeToClient() => context.Touch("subscribe-shutdown");
        public void Publish(int exitCode) => EngineShutdownEvent?.Invoke(exitCode);
    }

    private sealed class TestEngineLogger(TestContext context) : IEngineLogger
    {
        public void SubscribeToClient() => context.Touch("subscribe-log");
    }

    private sealed class TestEngineInput(TestContext context) : IEngineInputService
    {
        public event Action<EngineEditorInputEvent>? InputReceivedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public void SubscribeToClient() => context.Touch("subscribe-input");
    }

    private sealed class TestWindowController(TestContext context) : IEngineWindowController
    {
        public ReiEditor.Utils.Common.IObservable<IntPtr?> WindowPointer => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<(int Width, int Height)?> ViewportSize => throw new NotSupportedException();
        public void SetupWindow() => context.Touch("window");
        public void DestroyWindow() => context.Calls.Enqueue("window-destroy");
        public void SetViewportSize(int width, int height) => throw new NotSupportedException();
    }

    private sealed class TestContext : IAsyncDisposable
    {
        private bool _workerExpected;

        public TemporaryProjectFixture Project { get; } = new();
        public ConcurrentQueue<string> Calls { get; } = new();
        public TestLogger<EngineRunner> Logger { get; } = new();
        public EditorProceduresService Procedures { get; } = new();
        public TestRunnerApi Api { get; }
        public TestDllManager Dll { get; }
        public TestShutdownListener Shutdown { get; }
        public EngineRunner Runner { get; }
        public string? FailedStage { get; set; }
        public bool LoadSucceeded { get; set; }
        public TaskCompletionSource Release { get; private set; } = Signal();
        public TaskCompletionSource Started { get; private set; } = Signal();
        public TaskCompletionSource Failed { get; private set; } = Signal();
        public TaskCompletionSource Unloaded { get; private set; } = Signal();
        public TaskCompletionSource ProcedureFinished { get; private set; } = Signal();

        public TestContext()
        {
            Api = new(this);
            Dll = new(this);
            Shutdown = new(this);
            Runner = new(Api, Logger, new TestEngineLogger(this), new TestEngineInput(this), new TestWindowController(this), Shutdown, Project.Resources, Dll, Procedures);
            Runner.EngineStartedEvent += () => Started.TrySetResult();
            Runner.EngineStartFailedEvent += () => Failed.TrySetResult();
            Procedures.ProcedureFinishedEvent += _ => ProcedureFinished.TrySetResult();
        }

        public void Touch(string stage)
        {
            Calls.Enqueue(stage);
            if (stage == FailedStage) throw new IOException("controlled " + stage + " failure");
        }

        public bool Start(EngineRunMode mode)
        {
            var accepted = Runner.StartEngine(mode);
            _workerExpected |= accepted;
            return accepted;
        }

        public async Task AwaitStarted()
        {
            await Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await ProcedureFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void ResetSignals()
        {
            _workerExpected = false;
            LoadSucceeded = false;
            Release = Signal();
            Started = Signal();
            Failed = Signal();
            Unloaded = Signal();
            ProcedureFinished = Signal();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                try
                {
                    if (_workerExpected)
                    {
                        // Drain startup before shutdown so a queued callback cannot reactivate the runner.
                        await Task.WhenAny(Started.Task, Failed.Task).WaitAsync(TimeSpan.FromSeconds(5));
                        if (Started.Task.IsCompleted) await ProcedureFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                finally
                {
                    Shutdown.Publish(0);
                    Release.TrySetResult();
                    if (LoadSucceeded) await Unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            finally
            {
                try
                {
                    await Runner.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                }
                finally
                {
                    Project.Dispose();
                }
            }
        }

        private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Managed startup selects mode, subscribes callbacks and ends its procedure; stop destroys and unloads the engine.</summary>
    [Theory]
    [InlineData(EngineRunMode.EditorMode)]
    [InlineData(EngineRunMode.PlayMode)]
    public async Task StartsAndStopsSelectedMode(EngineRunMode mode)
    {
        await using var context = new TestContext();

        Assert.True(context.Start(mode));
        await context.AwaitStarted();

        Assert.Equal(mode, context.Runner.ActiveMode);
        Assert.True(context.Runner.IsActive.Value);
        Assert.Equal(mode == EngineRunMode.PlayMode, context.Runner.IsPlaymodeActive.Value);
        Assert.Equal(mode == EngineRunMode.EditorMode, context.Runner.IsEditorActive.Value);
        Assert.False(context.Runner.IsEngineStarting.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Equal((Path.Combine(context.Project.Directory.RootPath, "bin", "Resources"), mode), context.Api.Creation!.Value);
        Assert.Equal(new[] { "load", "create", "callback", "subscribe-log", "subscribe-input", "subscribe-shutdown", "window", "start" }, context.Calls);

        await context.Runner.StopEngine().WaitAsync(TimeSpan.FromSeconds(5));
        await context.Unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(context.Runner.IsActive.Value);
        Assert.False(context.Runner.IsPlaymodeActive.Value);
        Assert.False(context.Runner.IsEditorActive.Value);
        Assert.False(context.Dll.Loaded.Value);
        Assert.Equal(new[] { "shutdown", "window-destroy", "destroy", "unload" }, context.Calls.Skip(8));
    }

    /// <summary>A running engine rejects another start without creating a second procedure or engine instance.</summary>
    [Fact]
    public async Task RejectsStartWhileEnginePointerExists()
    {
        await using var context = new TestContext();
        Assert.True(context.Start(EngineRunMode.EditorMode));
        await context.AwaitStarted();
        var calls = context.Calls.ToArray();

        Assert.False(context.Start(EngineRunMode.PlayMode));

        Assert.Equal(calls, context.Calls);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Equal(EngineRunMode.EditorMode, context.Runner.ActiveMode);
    }

    /// <summary>DLL, creation, window and start failures reset state, clean owned resources and permit a later start.</summary>
    [Theory]
    [InlineData("load")]
    [InlineData("create")]
    [InlineData("window")]
    [InlineData("start")]
    public async Task StartFailureResetsStateAndAllowsRetry(string failedStage)
    {
        await using var context = new TestContext { FailedStage = failedStage };

        Assert.True(context.Start(EngineRunMode.PlayMode));
        await context.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (failedStage != "load") await context.Unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(context.Runner.IsActive.Value);
        Assert.False(context.Runner.IsEngineStarting.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.False(context.Dll.Loaded.Value);
        Assert.Contains(context.Logger.Entries, entry => entry.Exception is IOException && entry.Exception.Message == "controlled " + failedStage + " failure");
        Assert.Equal(failedStage is "window" or "start", context.Calls.Contains("destroy"));
        Assert.Contains("stopped", context.Calls);
        context.FailedStage = null;
        context.ResetSignals();

        Assert.True(context.Start(EngineRunMode.EditorMode));
        await context.AwaitStarted();
        Assert.True(context.Runner.IsEditorActive.Value);
    }

    /// <summary>Stopping an unused runner is a no-op and disposing it detaches the shutdown listener.</summary>
    [Fact]
    public async Task InactiveStopAndDisposedShutdownDoNotTouchDependencies()
    {
        await using var context = new TestContext();
        await context.Runner.StopEngine();
        await context.Runner.DisposeAsync();

        context.Shutdown.Publish(17);

        Assert.Empty(context.Calls);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }
}
