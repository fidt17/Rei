using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies build orchestration, stage selection, failures and cancellation using controlled managed dependencies.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class BuildServiceTests
{
    private sealed class TestEngineSettingsProvider(string root) : IEngineSettingsProvider
    {
        public string GetEnginePath() => root;
        public Task InitializeAsync() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => throw new NotSupportedException();
        public string GetEngineReleaseIncludeDir() => throw new NotSupportedException();
        public string GetEngineSourceIncludes() => throw new NotSupportedException();
        public string GetEngineResourcesDir() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => throw new NotSupportedException();
    }

    // One recorder captures ordering across the pipeline interfaces; each interface has only its test-required members enabled.
    private sealed class TestPipeline : IBuildPreparationService, ISolutionBuilder, IAssetBuilder, IProjectBuildStateService, IEditorConsoleService
    {
        public List<string> Calls { get; } = new();
        public Func<string, Task> OnStage { get; set; } = _ => Task.CompletedTask;
        public ProjectBuildState Evaluation { get; set; } = new(true, true, "changed");
        public (BuildConfigurationEnum Configuration, bool Clean, string? Output, CancellationToken Cancellation)? SolutionRequest { get; private set; }
        public (BuildExecutionContext Context, bool Force, Action<AssetBuildProgressInfo>? Progress)? AssetRequest { get; private set; }
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext Context, bool Solution, bool Assets)> Evaluations { get; } = new();
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext Context, bool Solution, bool Assets)> Saves { get; } = new();
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext Context)> Failures { get; } = new();
        public event Action<LogMessage> NewLogEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public event Action LogsClearedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public ReiEditor.Utils.Common.IObservable<int> LogsCount => throw new NotSupportedException();
        public IEnumerable<LogMessage> Logs => throw new NotSupportedException();

        public Task Stage(string stage) { Calls.Add(stage); return OnStage(stage); }
        public Task Prepare(CancellationToken cancellationToken) => Stage("prepare");
        public Task Build(BuildConfigurationEnum configuration, bool cleanBuild = false, string? outputDirectory = null, CancellationToken cancellationToken = default)
        {
            SolutionRequest = (configuration, cleanBuild, outputDirectory, cancellationToken);
            return Stage("solution");
        }
        public Task BuildAssets(BuildExecutionContext buildContext, bool forceRebuild = false, Action<AssetBuildProgressInfo>? onAssetBuilding = null)
        {
            AssetRequest = (buildContext, forceRebuild, onAssetBuilding);
            return Stage("assets");
        }
        public async Task<ProjectBuildState> CalculateState(BuildConfigurationEnum configuration, BuildExecutionContext buildContext, bool buildSolution, bool buildAssets)
        {
            Evaluations.Add((configuration, buildContext, buildSolution, buildAssets));
            await Stage("evaluate");
            return Evaluation;
        }
        public void MarkBuildStarted(BuildConfigurationEnum configuration, BuildExecutionContext buildContext) => Calls.Add("started");
        public void MarkBuildFailed(BuildConfigurationEnum configuration, BuildExecutionContext buildContext)
        {
            Calls.Add("failed");
            Failures.Add((configuration, buildContext));
        }
        public Task SaveSuccessfulBuild(BuildConfigurationEnum configuration, BuildExecutionContext buildContext, bool buildSolution, bool buildAssets)
        {
            Saves.Add((configuration, buildContext, buildSolution, buildAssets));
            return Stage("persist");
        }
        public void ClearConsole() => Calls.Add("clear");
        public void Log(LogMessage message) => throw new NotSupportedException();
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public TemporaryProjectFixture Project { get; } = new();
        public TestPipeline Pipeline { get; } = new();
        public EditorProceduresService Procedures { get; } = new();
        public TestLogger<BuildService> Logger { get; } = new();
        public BuildService Service { get; }

        public TestContext(bool validSources = true)
        {
            var engine = Project.Directory.GetPath("EngineSources");
            Directory.CreateDirectory(engine);
            Directory.CreateDirectory(Project.Resources.GetScriptsPath());
            var sources = new SourceFilesUtility(Project.Resources, new TestEngineSettingsProvider(engine), new TestLogger<SourceFilesUtility>());
            if (validSources) sources.ProcessFiles();
            var importer = new TestAssetImporter
            {
                OnImport = async () => { await Pipeline.Stage("import"); return new List<AssetInfo>(); }
            };
            Service = new(Project.Resources, Pipeline, Pipeline, Pipeline, Pipeline, Logger, Pipeline, importer, sources, Procedures);
        }

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            Project.Dispose();
        }
    }

    /// <summary>Successful build forwards context and flags, awaits each stage, persists success and resets procedure state.</summary>
    [Fact]
    public async Task SuccessfulBuildForwardsInputsAndPublishesReadyState()
    {
        await using var context = new TestContext();
        var execution = new BuildExecutionContext(context.Project.Directory.GetPath("Output"), context.Project.Directory.GetPath("Client"));
        using var cancellation = new CancellationTokenSource();
        Action<AssetBuildProgressInfo> progress = _ => { };
        var changes = new List<bool>();
        context.Service.BuildInProgress.Subscribe(changes.Add, invoke: false);

        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, true, true, true, execution, true, true, progress, cancellation.Token));

        Assert.Equal(new[] { "import", "prepare", "evaluate", "started", "solution", "assets", "persist", "clear" }, context.Pipeline.Calls);
        var solution = context.Pipeline.SolutionRequest!.Value;
        Assert.Equal((BuildConfigurationEnum.EditorDebug, true, execution.SolutionOutputDirectory, cancellation.Token), solution);
        var assets = context.Pipeline.AssetRequest!.Value;
        Assert.Same(execution, assets.Context);
        Assert.True(assets.Force);
        Assert.Same(progress, assets.Progress);
        Assert.Equal((BuildConfigurationEnum.EditorDebug, execution, true, true), Assert.Single(context.Pipeline.Saves));
        Assert.True(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Equal(new[] { true, false }, changes);
    }

    /// <summary>Stage masks and evaluated changes select only required work; force cannot override disabled stages.</summary>
    [Theory]
    [InlineData(true, true, true, false, false, true, false)]
    [InlineData(true, true, false, true, false, false, true)]
    [InlineData(true, true, false, false, true, true, true)]
    [InlineData(false, true, true, true, true, false, true)]
    [InlineData(true, false, true, true, true, true, false)]
    [InlineData(false, false, true, true, true, false, false)]
    public async Task SelectsRequiredStages(bool buildSolution, bool buildAssets, bool dirtySolution, bool dirtyAssets, bool force, bool expectedSolution, bool expectedAssets)
    {
        await using var context = new TestContext();
        context.Pipeline.Evaluation = new(dirtySolution, dirtyAssets, "test selection");

        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, forceSolutionRebuild: force, forceAssetRebuild: force, buildSolution: buildSolution, buildAssets: buildAssets));

        Assert.Equal(expectedSolution, context.Pipeline.Calls.Contains("solution"));
        Assert.Equal(expectedAssets, context.Pipeline.Calls.Contains("assets"));
        var evaluation = Assert.Single(context.Pipeline.Evaluations);
        Assert.Equal(buildSolution, evaluation.Solution);
        Assert.Equal(buildAssets, evaluation.Assets);
        Assert.Equal(Path.Combine(context.Project.Directory.RootPath, "bin"), evaluation.Context.BuildFolder);
        Assert.Equal(expectedSolution || expectedAssets, context.Pipeline.Calls.Contains("persist"));
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>An up-to-date build prepares and evaluates but invokes no compiler, asset builder or success persistence.</summary>
    [Fact]
    public async Task UpToDateBuildSkipsWorkAndBecomesReady()
    {
        await using var context = new TestContext();
        context.Pipeline.Evaluation = new(false, false, "unchanged");

        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));

        Assert.Equal(new[] { "import", "prepare", "evaluate" }, context.Pipeline.Calls);
        Assert.True(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Invalid source definitions prevent preparation and compilation and mark the build failed.</summary>
    [Fact]
    public async Task InvalidSourcesStopBeforePreparation()
    {
        await using var context = new TestContext(validSources: false);

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));

        Assert.Equal(new[] { "import", "failed" }, context.Pipeline.Calls);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Every asynchronous stage failure stops subsequent work, releases state and permits a successful retry.</summary>
    [Theory]
    [InlineData("import")]
    [InlineData("prepare")]
    [InlineData("evaluate")]
    [InlineData("solution")]
    [InlineData("assets")]
    [InlineData("persist")]
    public async Task FailedStageResetsStateAndAllowsRetry(string failedStage)
    {
        await using var context = new TestContext();
        var failure = new IOException("controlled " + failedStage + " failure");
        context.Pipeline.OnStage = stage => stage == failedStage ? Task.FromException(failure) : Task.CompletedTask;

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));

        Assert.Equal(failedStage, context.Pipeline.Calls[^2]);
        Assert.Equal("failed", context.Pipeline.Calls[^1]);
        Assert.Single(context.Pipeline.Failures);
        Assert.Contains(context.Logger.Entries, entry => ReferenceEquals(entry.Exception, failure));
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        context.Pipeline.OnStage = _ => Task.CompletedTask;
        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
    }

    /// <summary>A pre-canceled build does not import assets or invoke build stages and releases its procedure.</summary>
    [Fact]
    public async Task PreCanceledBuildDoesNoWork()
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token));

        Assert.Equal(new[] { "failed" }, context.Pipeline.Calls);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Cancellation at a stage boundary stops later work, prevents success persistence and releases build state.</summary>
    [Theory]
    [InlineData("import")]
    [InlineData("prepare")]
    [InlineData("evaluate")]
    [InlineData("solution")]
    [InlineData("assets")]
    public async Task CancellationDuringStagePreventsSuccess(string canceledStage)
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Pipeline.OnStage = stage => { if (stage == canceledStage) cancellation.Cancel(); return Task.CompletedTask; };
        var finishes = 0;
        context.Procedures.ProcedureFinishedEvent += _ => finishes++;

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token));

        Assert.Empty(context.Pipeline.Saves);
        Assert.Equal(canceledStage, context.Pipeline.Calls[^2]);
        Assert.Equal("failed", context.Pipeline.Calls[^1]);
        Assert.Single(context.Pipeline.Failures);
        Assert.DoesNotContain("clear", context.Pipeline.Calls);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Equal(1, finishes);
    }

    /// <summary>An unchanged build canceled during preparation or evaluation cannot report a successful skip.</summary>
    [Theory]
    [InlineData("prepare")]
    [InlineData("evaluate")]
    public async Task CanceledUpToDateBuildDoesNotBecomeReady(string canceledStage)
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Pipeline.Evaluation = new(false, false, "unchanged");
        context.Pipeline.OnStage = stage => { if (stage == canceledStage) cancellation.Cancel(); return Task.CompletedTask; };

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token));

        Assert.Equal(canceledStage, context.Pipeline.Calls[^2]);
        Assert.Equal("failed", context.Pipeline.Calls[^1]);
        Assert.DoesNotContain("started", context.Pipeline.Calls);
        Assert.Empty(context.Pipeline.Saves);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Solution-only cancellation is observed before persistence even when the assets stage is disabled.</summary>
    [Fact]
    public async Task CanceledSolutionOnlyBuildDoesNotPersistSuccess()
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Pipeline.OnStage = stage => { if (stage == "solution") cancellation.Cancel(); return Task.CompletedTask; };

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, buildAssets: false, cancellationToken: cancellation.Token));

        Assert.DoesNotContain("assets", context.Pipeline.Calls);
        Assert.Empty(context.Pipeline.Saves);
        Assert.Single(context.Pipeline.Failures);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Cancellation during success persistence marks the result failed and permits a fresh successful retry.</summary>
    [Fact]
    public async Task CancellationDuringPersistenceInvalidatesResultAndAllowsRetry()
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        context.Pipeline.OnStage = stage => { if (stage == "persist") cancellation.Cancel(); return Task.CompletedTask; };

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token));

        var save = Assert.Single(context.Pipeline.Saves);
        var failure = Assert.Single(context.Pipeline.Failures);
        Assert.Equal(save.Configuration, failure.Configuration);
        Assert.Same(save.Context, failure.Context);
        Assert.Equal(new[] { "persist", "failed" }, context.Pipeline.Calls.TakeLast(2));
        Assert.DoesNotContain("clear", context.Pipeline.Calls);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);

        context.Pipeline.OnStage = _ => Task.CompletedTask;
        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
        Assert.True(context.Service.IsBuildReady.Value);
        Assert.Equal(2, context.Pipeline.Saves.Count);
        Assert.Single(context.Pipeline.Failures);
    }

    /// <summary>Cancellation waits for the non-cancellable asset task to finish before releasing the procedure and reporting failure.</summary>
    [Fact]
    public async Task CancellationDuringPendingAssetsAwaitsCleanup()
    {
        await using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Pipeline.OnStage = stage => stage == "assets" ? gate.Task : Task.CompletedTask;
        var pending = context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token);
        try
        {
            Assert.Equal("assets", context.Pipeline.Calls.Last());
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.True(context.Service.BuildInProgress.Value);
            Assert.Single(context.Procedures.ActiveProcedures);
            Assert.Empty(context.Pipeline.Saves);
        }
        finally
        {
            gate.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(await pending);
        Assert.Empty(context.Pipeline.Saves);
        Assert.Single(context.Pipeline.Failures);
        Assert.False(context.Service.IsBuildReady.Value);
        Assert.False(context.Service.BuildInProgress.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>An in-flight build rejects reentry and keeps its procedure active until controlled work finishes.</summary>
    [Fact]
    public async Task RejectsConcurrentBuildWithoutStartingAnotherPipeline()
    {
        await using var context = new TestContext();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Pipeline.OnStage = stage => stage == "solution" ? gate.Task : Task.CompletedTask;
        var build = context.Service.BuildProject(BuildConfigurationEnum.EditorDebug);
        try
        {
            Assert.False(build.IsCompleted);
            Assert.True(context.Service.BuildInProgress.Value);
            Assert.Single(context.Procedures.ActiveProcedures);
            var calls = context.Pipeline.Calls.ToArray();
            Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
            Assert.Equal(calls, context.Pipeline.Calls);
            gate.SetResult();
            Assert.True(await build.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            gate.TrySetResult();
            await build.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Disposal waits for controlled build completion and discards the result instead of exposing it as ready.</summary>
    [Fact]
    public async Task DisposalDiscardsInFlightBuild()
    {
        await using var context = new TestContext();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Pipeline.OnStage = stage => stage == "solution" ? gate.Task : Task.CompletedTask;
        var build = context.Service.BuildProject(BuildConfigurationEnum.EditorDebug);
        var disposal = context.Service.DisposeAsync().AsTask();
        try
        {
            Assert.False(disposal.IsCompleted);
            gate.SetResult();
            Assert.False(await build.WaitAsync(TimeSpan.FromSeconds(5)));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(context.Service.IsBuildReady.Value);
            Assert.Empty(context.Procedures.ActiveProcedures);
        }
        finally
        {
            gate.TrySetResult();
            await build.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
