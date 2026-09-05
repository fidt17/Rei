using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Tests.Models.EditorApp.Refresh;

/// <summary>Verifies asset import refresh rebuild guards, outcomes, serialization, and disposal.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Refresh")]
public sealed class AssetImportEditorRefreshServiceTests
{
    /// <summary>Publishes controlled import completion events.</summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public Observable<bool> Importing { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting => Importing;
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
        public void PublishImported() => ImportedAssetsEvent?.Invoke();
    }

    /// <summary>Exposes a fixed build eligibility value.</summary>
    private sealed class TestCondition(bool value) : ICondition
    {
        public Observable<bool> Value { get; } = new(value);
        public ReiEditor.Utils.Common.IObservable<bool> IsTrue => Value;
        public void Dispose() { }
    }

    /// <summary>Controls editor rebuild completion without invoking build infrastructure.</summary>
    private sealed class TestBuildStarter(bool canStart) : IBuildStarter
    {
        private readonly TestCondition _condition = new(canStart);
        public ICondition CanStartBuild => _condition;
        public List<BuildConfigurationEnum> Requests { get; } = new();
        public Func<Task<bool>> OnBuild { get; set; } = () => Task.FromResult(true);
        public Task<bool> BuildProject(BuildConfigurationEnum configurationEnum, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(configurationEnum);
            return OnBuild();
        }
    }

    /// <summary>Controls build-in-progress state.</summary>
    private sealed class TestBuildService : IBuildService
    {
        public Observable<bool> InProgress { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> BuildInProgress => InProgress;
        public ReiEditor.Utils.Common.IObservable<bool> IsBuildReady => throw new NotSupportedException();
        public Task<bool> BuildProject(BuildConfigurationEnum configuration, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Records editor mode starts.</summary>
    private sealed class TestEditorModeStarter : IEditorModeStarter
    {
        public ICondition CanStart => throw new NotSupportedException();
        public int Starts { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Start() { Starts++; Started.TrySetResult(); }
    }

    /// <summary>Supplies engine state not used by this workflow.</summary>
    private sealed class TestEngineRunner : IEngineRunner
    {
        public event Action EngineStartedEvent = delegate { };
        public event Action EngineStartFailedEvent = delegate { };
        public ReiEditor.Utils.Common.IObservable<bool> IsActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => throw new NotSupportedException();
        public EngineRunMode ActiveMode => throw new NotSupportedException();
        public bool StartEngine(EngineRunMode mode) { EngineStartedEvent?.Invoke(); return true; }
        public Task StopEngine() { EngineStartFailedEvent?.Invoke(); return Task.CompletedTask; }
    }

    /// <summary>A successful eligible import rebuilds EditorDebug and starts editor mode.</summary>
    [Fact]
    public async Task SuccessfulImportRebuildStartsEditorMode()
    {
        var importer = new TestAssetImporter();
        var buildStarter = new TestBuildStarter(canStart: true);
        var editorStarter = new TestEditorModeStarter();
        using var service = new AssetImportEditorRefreshService(importer, buildStarter, new TestBuildService(), editorStarter, new TestEngineRunner(), new TestLogger<AssetImportEditorRefreshService>());

        importer.PublishImported();
        await editorStarter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { BuildConfigurationEnum.EditorDebug }, buildStarter.Requests);
        Assert.Equal(1, editorStarter.Starts);
    }

    /// <summary>An import during an active build is rejected before scheduling a rebuild.</summary>
    [Fact]
    public void BuildInProgressDropsImportRefresh()
    {
        var importer = new TestAssetImporter();
        var buildStarter = new TestBuildStarter(canStart: true);
        var buildService = new TestBuildService();
        var editorStarter = new TestEditorModeStarter();
        buildService.InProgress.Value = true;
        using var service = new AssetImportEditorRefreshService(importer, buildStarter, buildService, editorStarter, new TestEngineRunner(), new TestLogger<AssetImportEditorRefreshService>());

        importer.PublishImported();

        Assert.Empty(buildStarter.Requests);
        Assert.Equal(0, editorStarter.Starts);
    }

    /// <summary>An exception from rebuild is logged and does not start editor mode.</summary>
    [Fact]
    public async Task RebuildExceptionIsLoggedWithoutEditorStart()
    {
        var importer = new TestAssetImporter();
        var buildStarter = new TestBuildStarter(canStart: true);
        var failure = new InvalidOperationException("controlled rebuild failure");
        buildStarter.OnBuild = () => Task.FromException<bool>(failure);
        var editorStarter = new TestEditorModeStarter();
        var logger = new TestLogger<AssetImportEditorRefreshService>();
        using var service = new AssetImportEditorRefreshService(importer, buildStarter, new TestBuildService(), editorStarter, new TestEngineRunner(), logger);

        importer.PublishImported();
        await TestWaitForAsync(() => logger.Entries.Count == 1);

        Assert.Same(failure, Assert.Single(logger.Entries).Exception);
        Assert.Equal(0, editorStarter.Starts);
    }

    /// <summary>A second import while rebuild is held is dropped.</summary>
    [Fact]
    public async Task ImportDuringHeldRebuildIsDropped()
    {
        var importer = new TestAssetImporter();
        var buildStarter = new TestBuildStarter(canStart: true);
        var buildEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var buildGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        buildStarter.OnBuild = () => { buildEntered.TrySetResult(); return buildGate.Task; };
        var editorStarter = new TestEditorModeStarter();
        using var service = new AssetImportEditorRefreshService(importer, buildStarter, new TestBuildService(), editorStarter, new TestEngineRunner(), new TestLogger<AssetImportEditorRefreshService>());

        try
        {
            importer.PublishImported();
            await buildEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            importer.PublishImported();
            Assert.Single(buildStarter.Requests);
        }
        finally
        {
            buildGate.TrySetResult(true);
            await editorStarter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Single(buildStarter.Requests);
    }

    /// <summary>Disposal removes import completion subscription.</summary>
    [Fact]
    public void DisposeStopsImportRefreshes()
    {
        var importer = new TestAssetImporter();
        var buildStarter = new TestBuildStarter(canStart: true);
        var service = new AssetImportEditorRefreshService(importer, buildStarter, new TestBuildService(), new TestEditorModeStarter(), new TestEngineRunner(), new TestLogger<AssetImportEditorRefreshService>());
        service.Dispose();

        importer.PublishImported();

        Assert.Empty(buildStarter.Requests);
    }

    /// <summary>Waits for asynchronous worker terminal evidence without fixed sleeps.</summary>
    private static async Task TestWaitForAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
