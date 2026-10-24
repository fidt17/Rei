using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.EditorApp.Console;

/// <summary>Verifies console clearing follows build and playmode state subscriptions.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Console")]
public sealed class ClearConsoleOnEventsSystemTests
{
    /// <summary>Records clear requests without storing messages.</summary>
    private sealed class TestConsoleService : IEditorConsoleService
    {
        public event Action<LogMessage> NewLogEvent = delegate { };
        public event Action LogsClearedEvent = delegate { };
        public Observable<int> Count { get; } = new(0);
        public ReiEditor.Utils.Common.IObservable<int> LogsCount => Count;
        public IEnumerable<LogMessage> Logs => Array.Empty<LogMessage>();
        public int ClearCalls { get; private set; }
        public void Log(LogMessage message) => NewLogEvent?.Invoke(message);
        public void ClearConsole() { ClearCalls++; LogsClearedEvent?.Invoke(); }
    }

    /// <summary>Controls build state without executing builds.</summary>
    private sealed class TestBuildService : IBuildService
    {
        public Observable<bool> InProgress { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> BuildInProgress => InProgress;
        public ReiEditor.Utils.Common.IObservable<bool> IsBuildReady => throw new NotSupportedException();
        public Task<bool> BuildProject(BuildConfigurationEnum configuration, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Controls playmode state without executing engine code.</summary>
    private sealed class TestEngineRunner : IEngineRunner
    {
        public event Action EngineStartedEvent = delegate { };
        public event Action EngineStartFailedEvent = delegate { };
        public Observable<bool> Starting { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => Starting;
        public EngineRunMode ActiveMode { get; set; } = EngineRunMode.PlayMode;
        public bool StartEngine(EngineRunMode mode) { EngineStartedEvent?.Invoke(); return true; }
        public Task StopEngine() { EngineStartFailedEvent?.Invoke(); return Task.CompletedTask; }
    }

    /// <summary>Entering build or playmode clears console while false state changes do not.</summary>
    [Fact]
    public void BuildAndPlaymodeStartClearConsoleBeforeWork()
    {
        var console = new TestConsoleService();
        var build = new TestBuildService();
        var runner = new TestEngineRunner();
        using var system = new ClearConsoleOnEventsSystem(build, console, runner);

        build.InProgress.Value = true;
        build.InProgress.Value = false;
        runner.Starting.Value = true;
        runner.Starting.Value = false;

        Assert.Equal(2, console.ClearCalls);
    }

    /// <summary>Initially active states clear console immediately when subscriptions are created.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InitiallyActiveStateClearsConsoleOnConstruction(bool buildActive, bool engineStarting)
    {
        var console = new TestConsoleService();
        var build = new TestBuildService();
        var runner = new TestEngineRunner();
        build.InProgress.Value = buildActive;
        runner.Starting.Value = engineStarting;

        using var system = new ClearConsoleOnEventsSystem(build, console, runner);

        Assert.Equal((buildActive ? 1 : 0) + (engineStarting ? 1 : 0), console.ClearCalls);
    }

    /// <summary>Disposal removes both state subscriptions.</summary>
    [Fact]
    public void DisposeStopsClearingConsole()
    {
        var console = new TestConsoleService();
        var build = new TestBuildService();
        var runner = new TestEngineRunner();
        var system = new ClearConsoleOnEventsSystem(build, console, runner);
        system.Dispose();

        build.InProgress.Value = true;
        runner.Starting.Value = true;

        Assert.Equal(0, console.ClearCalls);
    }

    [Fact]
    public void PlaymodeReadinessPreservesLogsWrittenDuringStartup()
    {
        var console = new EditorConsoleService();
        var runner = new TestEngineRunner();
        using var system = new ClearConsoleOnEventsSystem(new TestBuildService(), console, runner);
        console.Log(new(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "old", ""));
        runner.Starting.Value = true;
        Assert.Empty(console.Logs);
        console.Log(new(LogScopeEnum.Engine, LogLevelEnum.Info, DateTime.UtcNow, "startup complete", ""));
        runner.Starting.Value = false;
        Assert.Equal("startup complete", Assert.Single(console.Logs).Message);
    }

    [Fact]
    public void EditorStartPreservesBuildSummary()
    {
        var console = new EditorConsoleService();
        var runner = new TestEngineRunner { ActiveMode = EngineRunMode.EditorMode };
        using var system = new ClearConsoleOnEventsSystem(new TestBuildService(), console, runner);
        console.Log(new(LogScopeEnum.Editor, LogLevelEnum.Info, DateTime.UtcNow, "build summary", ""));
        runner.Starting.Value = true;
        Assert.Equal("build summary", Assert.Single(console.Logs).Message);
    }
}
