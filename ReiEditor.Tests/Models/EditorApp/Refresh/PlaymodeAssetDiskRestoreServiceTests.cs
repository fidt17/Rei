using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.EditorApp.Refresh;

/// <summary>Verifies playmode exit restores loaded assets and refreshes editor safely.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Refresh")]
public sealed class PlaymodeAssetDiskRestoreServiceTests
{
    /// <summary>Records reload requests and supplies controlled completion.</summary>
    private sealed class TestAssetsService : IAssetsService
    {
        public Observable<bool> Saving { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => Saving;
        public List<IReadOnlyCollection<string>> ReloadRequests { get; } = new();
        public Func<Task> OnReload { get; set; } = () => Task.CompletedTask;
        public Task<T?> Load<T>(string assetId) where T : Asset => throw new NotSupportedException();
        public Task<T?> LoadFrom<T>(string projectPath) where T : Asset => throw new NotSupportedException();
        public Task<T?> Load<T>(AssetInfo assetInfo) where T : Asset => throw new NotSupportedException();
        public void Unload(string assetId) => throw new NotSupportedException();
        public Task SaveProject() => throw new NotSupportedException();
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions)
        {
            ReloadRequests.Add(ignoredExtensions);
            return OnReload();
        }
    }

    /// <summary>Publishes controlled playmode state changes.</summary>
    private sealed class TestEngineRunner : IEngineRunner
    {
        public event Action EngineStartedEvent = delegate { };
        public event Action EngineStartFailedEvent = delegate { };
        public Observable<bool> PlaymodeActive { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => throw new NotSupportedException();
        public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => PlaymodeActive;
        public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => throw new NotSupportedException();
        public EngineRunMode ActiveMode => throw new NotSupportedException();
        public bool StartEngine(EngineRunMode mode) { EngineStartedEvent?.Invoke(); return true; }
        public Task StopEngine() { EngineStartFailedEvent?.Invoke(); return Task.CompletedTask; }
    }

    /// <summary>Records refresh notifications and signals terminal success.</summary>
    private sealed class TestEditorRefreshService : IEditorRefreshService
    {
        public event Action RefreshedEvent = delegate { };
        public int Refreshes { get; private set; }
        public TaskCompletionSource Refreshed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void NotifyRefreshed() { Refreshes++; RefreshedEvent?.Invoke(); Refreshed.TrySetResult(); }
    }

    /// <summary>Leaving playmode reloads assets except scenes and then publishes editor refresh.</summary>
    [Fact]
    public async Task PlaymodeExitReloadsNonSceneAssetsThenRefreshes()
    {
        var assets = new TestAssetsService();
        var runner = new TestEngineRunner();
        var refresh = new TestEditorRefreshService();
        using var service = new PlaymodeAssetDiskRestoreService(assets, runner, refresh, new TestLogger<PlaymodeAssetDiskRestoreService>());
        runner.PlaymodeActive.Value = true;

        runner.PlaymodeActive.Value = false;
        await refresh.Refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var ignored = Assert.Single(assets.ReloadRequests);
        Assert.Equal(new[] { FileExtensions.SCENE }, ignored);
        Assert.Equal(1, refresh.Refreshes);
    }

    /// <summary>Entering playmode does not reload assets.</summary>
    [Fact]
    public void PlaymodeEntryDoesNotReloadAssets()
    {
        var assets = new TestAssetsService();
        var runner = new TestEngineRunner();
        using var service = new PlaymodeAssetDiskRestoreService(assets, runner, new TestEditorRefreshService(), new TestLogger<PlaymodeAssetDiskRestoreService>());

        runner.PlaymodeActive.Value = true;

        Assert.Empty(assets.ReloadRequests);
    }

    /// <summary>Reload failure is logged and does not publish editor refresh.</summary>
    [Fact]
    public async Task ReloadFailureIsLoggedWithoutRefresh()
    {
        var failure = new IOException("controlled reload failure");
        var assets = new TestAssetsService { OnReload = () => Task.FromException(failure) };
        var runner = new TestEngineRunner();
        var refresh = new TestEditorRefreshService();
        var logger = new TestLogger<PlaymodeAssetDiskRestoreService>();
        using var service = new PlaymodeAssetDiskRestoreService(assets, runner, refresh, logger);
        runner.PlaymodeActive.Value = true;

        runner.PlaymodeActive.Value = false;
        await TestWaitForAsync(() => logger.Entries.Count == 1);

        Assert.Same(failure, Assert.Single(logger.Entries).Exception);
        Assert.Equal(0, refresh.Refreshes);
    }

    /// <summary>A second playmode exit while restore is held is dropped.</summary>
    [Fact]
    public async Task PlaymodeExitDuringHeldRestoreIsDropped()
    {
        var restoreEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restoreGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var assets = new TestAssetsService
        {
            OnReload = () => { restoreEntered.TrySetResult(); return restoreGate.Task; }
        };
        var runner = new TestEngineRunner();
        var refresh = new TestEditorRefreshService();
        using var service = new PlaymodeAssetDiskRestoreService(assets, runner, refresh, new TestLogger<PlaymodeAssetDiskRestoreService>());
        runner.PlaymodeActive.Value = true;

        try
        {
            runner.PlaymodeActive.Value = false;
            await restoreEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runner.PlaymodeActive.Value = true;
            runner.PlaymodeActive.Value = false;
            Assert.Single(assets.ReloadRequests);
        }
        finally
        {
            restoreGate.TrySetResult();
            await refresh.Refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Single(assets.ReloadRequests);
    }

    /// <summary>Disposal removes playmode state subscription.</summary>
    [Fact]
    public void DisposeStopsPlaymodeExitRestore()
    {
        var assets = new TestAssetsService();
        var runner = new TestEngineRunner();
        var service = new PlaymodeAssetDiskRestoreService(assets, runner, new TestEditorRefreshService(), new TestLogger<PlaymodeAssetDiskRestoreService>());
        runner.PlaymodeActive.Value = true;
        service.Dispose();

        runner.PlaymodeActive.Value = false;

        Assert.Empty(assets.ReloadRequests);
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
