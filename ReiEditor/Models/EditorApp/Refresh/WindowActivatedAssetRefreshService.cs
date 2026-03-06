using System;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Refresh;

public sealed class WindowActivatedAssetRefreshService : IDisposable
{
    private readonly IMainWindowService _mainWindowService;
    private readonly ProjectFilesWatcherService _projectFilesWatcherService;
    private readonly IAssetImporter _assetImporter;
    private readonly IBuildService _buildService;
    private readonly IEngineRunner _engineRunner;
    private readonly ILogger<WindowActivatedAssetRefreshService> _logger;
    private readonly object _refreshLock = new();

    private bool _refreshInProgress;

    public WindowActivatedAssetRefreshService(
        IMainWindowService mainWindowService,
        ProjectFilesWatcherService projectFilesWatcherService,
        IAssetImporter assetImporter,
        IBuildService buildService,
        IEngineRunner engineRunner,
        ILogger<WindowActivatedAssetRefreshService> logger)
    {
        _mainWindowService = mainWindowService;
        _projectFilesWatcherService = projectFilesWatcherService;
        _assetImporter = assetImporter;
        _buildService = buildService;
        _engineRunner = engineRunner;
        _logger = logger;

        _mainWindowService.ActivatedEvent += HandleWindowActivatedEvent;
    }

    public void Dispose()
    {
        _mainWindowService.ActivatedEvent -= HandleWindowActivatedEvent;
    }

    private void HandleWindowActivatedEvent()
    {
        if (_assetImporter.IsImporting.Value) return;
        if (_buildService.BuildInProgress.Value) return;
        if (_engineRunner.IsPlaymodeActive.Value) return;
        if (_engineRunner.IsEngineStarting.Value) return;

        lock (_refreshLock)
        {
            if (_refreshInProgress) return;
            if (!_projectFilesWatcherService.ConsumePendingChangesWhileInactive()) return;

            _refreshInProgress = true;
        }

        _ = Task.Run(ReimportAssetsOnWindowActivated);
    }

    private async Task ReimportAssetsOnWindowActivated()
    {
        try
        {
            _logger.Log("Reimporting all assets after window activation due to external changes");
            await _assetImporter.ReimportAll();
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
        finally
        {
            lock (_refreshLock)
            {
                _refreshInProgress = false;
            }
        }
    }
}
