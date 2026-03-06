using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Refresh;

public sealed class AssetImportEditorRefreshService : IDisposable
{
    private readonly IAssetImporter _assetImporter;
    private readonly IBuildStarter _buildStarter;
    private readonly IBuildService _buildService;
    private readonly IEditorModeStarter _editorModeStarter;
    private readonly IEngineRunner _engineRunner;
    private readonly ILogger<AssetImportEditorRefreshService> _logger;

    private readonly object _refreshLock = new();
    private bool _refreshInProgress;

    public AssetImportEditorRefreshService(
        IAssetImporter assetImporter,
        IBuildStarter buildStarter,
        IBuildService buildService,
        IEditorModeStarter editorModeStarter,
        IEngineRunner engineRunner,
        ILogger<AssetImportEditorRefreshService> logger)
    {
        _assetImporter = assetImporter;
        _buildStarter = buildStarter;
        _buildService = buildService;
        _editorModeStarter = editorModeStarter;
        _engineRunner = engineRunner;
        _logger = logger;

        _assetImporter.ImportedAssetsEvent += HandleImportedAssetsEvent;
    }

    public void Dispose()
    {
        _assetImporter.ImportedAssetsEvent -= HandleImportedAssetsEvent;
    }

    private void HandleImportedAssetsEvent()
    {
        if (_buildService.BuildInProgress.Value) return;

        lock (_refreshLock)
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;
        }

        _ = Task.Run(RebuildAndRestartEditorMode);
    }

    private async Task RebuildAndRestartEditorMode()
    {
        try
        {
            if (_buildService.BuildInProgress.Value) return;
            if (!_buildStarter.CanStartBuild.IsTrue.Value) return;

            await _buildStarter.BuildProject(BuildConfigurationEnum.EditorDebug);
            _editorModeStarter.Start();
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
