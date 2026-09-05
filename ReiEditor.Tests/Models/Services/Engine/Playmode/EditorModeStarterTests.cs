using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine.Playmode;

/// <summary>Verifies editor-mode guards, stop/start routing, event activation and exception logging.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Playmode")]
public sealed class EditorModeStarterTests
{
    /// <summary>Records asynchronous editor-mode exceptions and exposes deterministic completion.</summary>
    private sealed class TestExceptionLogger : ILogger<EditorModeStarter>
    {
        public List<Exception> Exceptions { get; } = new();
        public TaskCompletionSource<Exception> ExceptionLogged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Rejects unused empty-line logging.</summary>
        public void AddEmptyLine() => throw new NotSupportedException();

        /// <summary>Rejects unused informational logging.</summary>
        public void Log(string message) => throw new NotSupportedException();

        /// <summary>Rejects unused warning logging.</summary>
        public void LogWarning(string message) => throw new NotSupportedException();

        /// <summary>Rejects unused error logging.</summary>
        public void LogError(string message) => throw new NotSupportedException();

        /// <summary>Records exception and completes test signal.</summary>
        public void LogException(Exception exception)
        {
            Exceptions.Add(exception);
            ExceptionLogged.TrySetResult(exception);
        }
    }

    /// <summary>Owns controlled dependencies and records asynchronous runner calls.</summary>
    private sealed class TestContext : IDisposable
    {
        public TestBuildService Build { get; } = new();
        public TestAssetsService Assets { get; } = new();
        public TestAssetImporter Importer { get; } = new();
        public TestEngineRunner Engine { get; } = new();
        public TestExceptionLogger Logger { get; } = new();
        public List<string> Calls { get; } = new();
        public EditorModeStarter Starter { get; }

        /// <summary>Creates an eligible service whose runner signals each stop and start.</summary>
        public TestContext()
        {
            Build.Ready.Value = true;
            Engine.OnStop = () => { Calls.Add("stop"); return Task.CompletedTask; };
            Engine.OnStart = mode => { Calls.Add("start:" + mode); return true; };
            Starter = new EditorModeStarter(Build, Logger, Assets, Importer, Engine);
        }

        /// <summary>Disposes guard and playmode subscriptions.</summary>
        public void Dispose() => Starter.Dispose();
    }

    /// <summary>Each busy state independently blocks editor-mode eligibility and clearing it restores eligibility.</summary>
    [Theory]
    [InlineData("build")]
    [InlineData("editor")]
    [InlineData("starting")]
    [InlineData("saving")]
    [InlineData("importing")]
    [InlineData("not-ready")]
    public void TestCanStartTracksAllGuards(string guard)
    {
        using var context = new TestContext();
        var state = guard switch
        {
            "build" => context.Build.InProgress,
            "editor" => context.Engine.EditorActive,
            "starting" => context.Engine.Starting,
            "saving" => context.Assets.Saving,
            "importing" => context.Importer.Importing,
            _ => context.Build.Ready
        };
        state.Value = guard != "not-ready";

        Assert.False(context.Starter.CanStart.IsTrue.Value);
        state.Value = guard == "not-ready";
        Assert.True(context.Starter.CanStart.IsTrue.Value);
    }

    /// <summary>Explicit start stops the current engine before requesting EditorMode.</summary>
    [Fact]
    public async Task TestStartStopsBeforeRequestingEditorMode()
    {
        using var context = new TestContext();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Engine.OnStart = mode => { context.Calls.Add("start:" + mode); started.TrySetResult(); return true; };

        context.Starter.Start();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { "stop", "start:" + EngineRunMode.EditorMode }, context.Calls);
    }

    /// <summary>Leaving playmode automatically enters editor mode through the same stop/start route.</summary>
    [Fact]
    public async Task TestPlaymodeExitStartsEditorMode()
    {
        using var context = new TestContext();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Engine.PlaymodeActive.Value = true;
        context.Engine.OnStart = mode => { context.Calls.Add("start:" + mode); started.TrySetResult(); return true; };

        context.Engine.PlaymodeActive.Value = false;
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { "stop", "start:" + EngineRunMode.EditorMode }, context.Calls);
    }

    /// <summary>Runner exceptions from the fire-and-forget command are captured by the service logger.</summary>
    [Fact]
    public async Task TestExceptionIsLogged()
    {
        using var context = new TestContext();
        var failure = new IOException("controlled stop failure");
        context.Engine.OnStop = () => Task.FromException(failure);

        context.Starter.Start();
        Assert.Same(failure, await context.Logger.ExceptionLogged.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(failure, Assert.Single(context.Logger.Exceptions));
    }

    /// <summary>Disposal detaches playmode-exit activation and freezes eligibility subscriptions.</summary>
    [Fact]
    public void TestDisposeDetachesSubscriptions()
    {
        var context = new TestContext();
        Assert.True(context.Starter.CanStart.IsTrue.Value);
        Assert.Equal(1, context.Engine.PlaymodeSubscriberCount);
        context.Starter.Dispose();

        context.Build.InProgress.Value = true;
        Assert.Equal(0, context.Engine.PlaymodeSubscriberCount);
        Assert.True(context.Starter.CanStart.IsTrue.Value);
        Assert.Empty(context.Calls);
    }
}
