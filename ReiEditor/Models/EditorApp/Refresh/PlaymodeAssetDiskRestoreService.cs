using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Refresh;

public sealed class PlaymodeAssetDiskRestoreService : IDisposable
{
    private readonly IAssetsService _assetsService;
    private readonly IEngineRunner _engineRunner;
    private readonly IEditorRefreshService _editorRefreshService;
    private readonly ILogger<PlaymodeAssetDiskRestoreService> _logger;
    private readonly object _restoreLock = new();

    private bool _restoreInProgress;

    public PlaymodeAssetDiskRestoreService(
        IAssetsService assetsService,
        IEngineRunner engineRunner,
        IEditorRefreshService editorRefreshService,
        ILogger<PlaymodeAssetDiskRestoreService> logger)
    {
        _assetsService = assetsService;
        _engineRunner = engineRunner;
        _editorRefreshService = editorRefreshService;
        _logger = logger;

        _engineRunner.IsPlaymodeActive.Subscribe(HandleIsPlaymodeActiveChanged, invoke: false);
    }

    public void Dispose()
    {
        _engineRunner.IsPlaymodeActive.Unsubscribe(HandleIsPlaymodeActiveChanged);
    }

    private void HandleIsPlaymodeActiveChanged(bool isPlaymodeActive)
    {
        if (isPlaymodeActive) return;

        lock (_restoreLock)
        {
            if (_restoreInProgress) return;
            _restoreInProgress = true;
        }

        _ = Task.Run(RestoreAssetsFromDisk);
    }

    private async Task RestoreAssetsFromDisk()
    {
        try
        {
            await _assetsService.ReloadLoadedAssetsFromDisk(new[] { FileExtensions.SCENE });
            _editorRefreshService.NotifyRefreshed();
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
        finally
        {
            lock (_restoreLock)
            {
                _restoreInProgress = false;
            }
        }
    }
}
