using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Condition;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Tests.Models.Services.Engine.Playmode;

/// <summary>Verifies stop, save, build and callback-based playmode startup with cancellation cleanup.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Playmode")]
public sealed class PlaymodeStartWorkflowTests
{
    /// <summary>Supplies one prebuilt command to workflow construction.</summary>
    private sealed class TestFactory(SaveProjectCommand command) : IFactory<SaveProjectCommand>
    {
        /// <summary>Returns controlled save command.</summary>
        public SaveProjectCommand CreateInstance() => command;

        /// <summary>Rejects parameterized construction unused by workflow.</summary>
        public SaveProjectCommand CreateInstance(params object[] parameters) => throw new NotSupportedException();
    }

    /// <summary>Records scene synchronization in workflow call order.</summary>
    private sealed class TestSynchronizer(List<string> calls) : ISceneStateSynchronizer
    {
        /// <summary>Records one engine-to-editor synchronization request.</summary>
        public void SynchronizeStateWithEngine() => calls.Add("synchronize");
    }

    /// <summary>Controls build result, cancellation and invocation records.</summary>
    private sealed class TestBuildStarter : IBuildStarter, IDisposable
    {
        private readonly List<string> _calls;
        private readonly Observable<bool> _canBuild = new(true);
        private readonly Condition _condition;

        public ICondition CanStartBuild => _condition;
        public List<CancellationToken> Tokens { get; } = new();
        public Func<CancellationToken, Task<bool>> OnBuild { get; set; } = _ => Task.FromResult(true);

        /// <summary>Creates always-eligible build control.</summary>
        public TestBuildStarter(List<string> calls)
        {
            _calls = calls;
            _condition = new Condition(_canBuild, true);
        }

        /// <summary>Records EditorDebug build and forwards controlled result.</summary>
        public Task<bool> BuildProject(BuildConfigurationEnum configurationEnum, bool forceSolutionRebuild = false,
            bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null,
            bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(BuildConfigurationEnum.EditorDebug, configurationEnum);
            Assert.False(forceSolutionRebuild || forceCleanSolutionBuild || forceAssetRebuild);
            Assert.Null(buildContext);
            Assert.True(buildSolution && buildAssets);
            Assert.Null(onAssetBuilding);
            _calls.Add("build");
            Tokens.Add(cancellationToken);
            return OnBuild(cancellationToken);
        }

        /// <summary>Disposes eligibility condition.</summary>
        public void Dispose() => _condition.Dispose();
    }

    /// <summary>Controls final playmode request and records mode-independent workflow ordering.</summary>
    private sealed class TestPlaymodeStarter : IPlaymodeStarter, IDisposable
    {
        private readonly List<string> _calls;
        private readonly Observable<bool> _canStart = new(true);
        private readonly Condition _condition;

        public ICondition CanStart => _condition;
        public Func<bool> OnStart { get; set; } = () => true;

        /// <summary>Creates always-eligible playmode control.</summary>
        public TestPlaymodeStarter(List<string> calls)
        {
            _calls = calls;
            _condition = new Condition(_canStart, true);
        }

        /// <summary>Records and invokes controlled start result.</summary>
        public bool TryStart()
        {
            _calls.Add("start");
            return OnStart();
        }

        /// <summary>Disposes eligibility condition.</summary>
        public void Dispose() => _condition.Dispose();
    }

    /// <summary>Owns real save command plus controlled workflow dependencies.</summary>
    private sealed class TestContext : IDisposable
    {
        public List<string> Calls { get; } = new();
        public TestAssetsService Assets { get; } = new();
        public TestBuildService SaveGuardBuild { get; } = new();
        public TestEngineRunner Engine { get; } = new();
        public TestBuildStarter Build { get; }
        public TestPlaymodeStarter Playmode { get; }
        public TestLogger<PlaymodeStartWorkflow> Logger { get; } = new();
        public SaveProjectCommand SaveCommand { get; }
        public PlaymodeStartWorkflow Workflow { get; }
        public int StopCount { get; private set; }

        /// <summary>Creates successful defaults while leaving engine completion callback under test control.</summary>
        public TestContext()
        {
            Assets.OnSave = () => { Calls.Add("save"); return Task.CompletedTask; };
            Engine.OnStop = () => { StopCount++; Calls.Add("stop"); return Task.CompletedTask; };
            Build = new TestBuildStarter(Calls);
            Playmode = new TestPlaymodeStarter(Calls);
            SaveCommand = new SaveProjectCommand(Assets, SaveGuardBuild, Engine, new TestSynchronizer(Calls));
            Workflow = new PlaymodeStartWorkflow(Logger, Build, Engine, Playmode, new TestFactory(SaveCommand));
        }

        /// <summary>Disposes workflow and test conditions.</summary>
        public void Dispose()
        {
            Workflow.Dispose();
            Playmode.Dispose();
            Build.Dispose();
        }
    }

    /// <summary>Already-active playmode succeeds without stop, save, build or start work.</summary>
    [AvaloniaFact]
    public async Task TestAlreadyActivePlaymodeReturnsTrueWithoutWork()
    {
        using var context = new TestContext();
        context.Engine.PlaymodeActive.Value = true;

        Assert.True(await context.Workflow.StartAsync());
        Assert.Empty(context.Calls);
    }

    /// <summary>Successful workflow preserves stop, synchronize, save, EditorDebug build and callback start order.</summary>
    [AvaloniaFact]
    public async Task TestSuccessfulStartRunsStagesInOrderAndForwardsToken()
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Playmode.OnStart = () => { context.Engine.PublishStarted(); return true; };

        Assert.True(await context.Workflow.StartAsync(cancellation.Token));

        Assert.Equal(new[] { "stop", "synchronize", "save", "build", "start" }, context.Calls);
        Assert.Equal(cancellation.Token, Assert.Single(context.Build.Tokens));
    }

    /// <summary>Save guard, failed build, rejected start and failed callback stop at their respective stages.</summary>
    [AvaloniaTheory]
    [InlineData("save")]
    [InlineData("build")]
    [InlineData("start")]
    [InlineData("callback")]
    public async Task TestStageFailureReturnsFalseAndAllowsRetry(string stage)
    {
        using var context = new TestContext();
        if (stage == "save") context.Assets.Saving.Value = true;
        if (stage == "build") context.Build.OnBuild = _ => Task.FromResult(false);
        if (stage == "start") context.Playmode.OnStart = () => false;
        if (stage == "callback") context.Playmode.OnStart = () => { context.Engine.PublishStartFailed(); return true; };

        Assert.False(await context.Workflow.StartAsync());
        if (stage != "callback")
            Assert.Contains(context.Logger.Entries, entry => entry.Level == ReiEditor.Models.Services.Logging.LogLevelEnum.Error);
        else
            Assert.Empty(context.Logger.Entries);

        context.Assets.Saving.Value = false;
        context.Build.OnBuild = _ => Task.FromResult(true);
        context.Playmode.OnStart = () => { context.Engine.PublishStarted(); return true; };
        Assert.True(await context.Workflow.StartAsync());
    }

    /// <summary>Exception is logged, false is returned and command guard is released for retry.</summary>
    [AvaloniaFact]
    public async Task TestExceptionIsLoggedAndAllowsRetry()
    {
        using var context = new TestContext();
        var failure = new IOException("controlled build failure");
        context.Build.OnBuild = _ => Task.FromException<bool>(failure);

        Assert.False(await context.Workflow.StartAsync());
        Assert.Same(failure, Assert.Single(context.Logger.Entries).Exception);

        context.Build.OnBuild = _ => Task.FromResult(true);
        context.Playmode.OnStart = () => { context.Engine.PublishStarted(); return true; };
        Assert.True(await context.Workflow.StartAsync());
    }

    /// <summary>Held stop keeps one command active, causing concurrent reentry to return false without duplicate work.</summary>
    [AvaloniaFact]
    public async Task TestConcurrentStartIsRejected()
    {
        using var context = new TestContext();
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Engine.OnStop = async () =>
        {
            context.Calls.Add("stop");
            stopEntered.TrySetResult();
            await stopGate.Task;
        };
        context.Playmode.OnStart = () => { context.Engine.PublishStarted(); return true; };
        var first = context.Workflow.StartAsync();
        try
        {
            await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(await context.Workflow.StartAsync());
            stopGate.SetResult();
            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, context.Calls.Count(call => call == "stop"));
        }
        finally
        {
            stopGate.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Cancellation after stop propagates and leaves command reusable.</summary>
    [AvaloniaFact]
    public async Task TestCancellationAfterStopPropagatesAndAllowsRetry()
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Engine.OnStop = () => { context.Calls.Add("stop"); cancellation.Cancel(); return Task.CompletedTask; };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Workflow.StartAsync(cancellation.Token));
        Assert.Equal(new[] { "stop" }, context.Calls);

        context.Engine.OnStop = () => Task.CompletedTask;
        context.Playmode.OnStart = () => { context.Engine.PublishStarted(); return true; };
        Assert.True(await context.Workflow.StartAsync());
    }

    /// <summary>Cancellation while startup is pending stops one late successful engine and removes cleanup handlers.</summary>
    [AvaloniaFact]
    public async Task TestLateSuccessAfterCancellationStopsEngineOnce()
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        var startRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Engine.Starting.Value = true;
        context.Playmode.OnStart = () => { startRequested.TrySetResult(); return true; };
        var pending = context.Workflow.StartAsync(cancellation.Token);
        await startRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        context.Engine.PublishStarted();
        context.Engine.PublishStarted();

        Assert.Equal(2, context.StopCount);
    }

    /// <summary>Late failure after cancellation removes cleanup handlers without stopping a later unrelated start.</summary>
    [AvaloniaFact]
    public async Task TestLateFailureAfterCancellationRemovesCleanupHandlers()
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        var startRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Engine.Starting.Value = true;
        context.Playmode.OnStart = () => { startRequested.TrySetResult(); return true; };
        var pending = context.Workflow.StartAsync(cancellation.Token);
        await startRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        context.Engine.PublishStartFailed();
        context.Engine.PublishStarted();

        Assert.Equal(1, context.StopCount);
    }

    /// <summary>Disposal frees real save command subscriptions so later guard changes raise no command notification.</summary>
    [AvaloniaFact]
    public async Task TestDisposeReleasesSaveCommandSubscriptions()
    {
        var context = new TestContext();
        var notifications = 0;
        context.SaveCommand.CanExecuteChanged += (_, _) => notifications++;

        context.Dispose();
        context.SaveGuardBuild.InProgress.Value = true;
        context.Engine.PlaymodeActive.Value = true;
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal(0, notifications);
    }
}
