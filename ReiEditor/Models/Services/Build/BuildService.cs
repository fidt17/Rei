using System;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Build;

public class BuildService : IBuildService
{
    public Utils.Common.IObservable<bool> BuildInProgress => _buildInProgress;
    public Utils.Common.IObservable<bool> IsBuildReady => _isBuildReady;

    private readonly Observable<bool> _buildInProgress = new(false);
    private readonly Observable<bool> _isBuildReady = new(false);

    private readonly IResourceService _resourceService;
    private readonly IAssetsService _assetsService;
    private readonly IAssetBuilder _assetBuilder;
    private readonly ISourceTracker _sourceTracker;
    private readonly ISolutionBuilder _solutionBuilder;
    private readonly IClientDllManager _clientDllManager;
    private readonly IEditorConsoleService _editorConsoleService;
    private readonly ILogger<BuildService> _logger;

    public BuildService(IResourceService resourceService, IAssetsService assetsService, IAssetBuilder assetBuilder, ISourceTracker sourceTracker, ISolutionBuilder solutionBuilder, IClientDllManager clientDllManager, ILogger<BuildService> logger, IEditorConsoleService editorConsoleService)
    {
        _resourceService = resourceService;
        _assetsService = assetsService;
        _assetBuilder = assetBuilder;
        _sourceTracker = sourceTracker;
        _solutionBuilder = solutionBuilder;
        _clientDllManager = clientDllManager;
        _logger = logger;
        _editorConsoleService = editorConsoleService;
    }

    public async Task<bool> BuildProject(BuildConfigurationEnum configuration)
    {
        if (_buildInProgress)
        {
            _logger.LogError("Another build in progress");
            return false;
        }

        var buildFolder = Path.Combine(_resourceService.GetRootPath(), "bin");
        _buildInProgress.Value = true;
        _isBuildReady.Value = false;
            
        try
        {
            await _assetsService.RefreshAssets();
            await _assetsService.SaveProject();

            if (!_clientDllManager.DllExists() || await _sourceTracker.ChangedOrNewSourcesExist())
            {
                await _solutionBuilder.Build(configuration);
            }
            
            await _assetBuilder.BuildAssets(buildFolder);
        }
        catch (Exception e)
        {
            _logger.LogError("Build Failed!");
            _logger.LogException(e);
            _isBuildReady.Value = false;
            _buildInProgress.Value = false;
            return false;
        }

        _editorConsoleService.ClearConsole();
        _logger.Log("Build Complete!");
        _isBuildReady.Value = true;
        _buildInProgress.Value = false;
        
        return false;
    }
}