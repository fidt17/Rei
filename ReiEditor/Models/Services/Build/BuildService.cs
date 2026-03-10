using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Models.Services.Engine.Dll;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Procedures;

namespace ReiEditor.Models.Services.Build;

public class BuildService : IBuildService, IAsyncDisposable
{
    public Utils.Common.IObservable<bool> BuildInProgress => _buildInProgress;
    public Utils.Common.IObservable<bool> IsBuildReady => _isBuildReady;

    private bool _discardBuild;

    private readonly Observable<bool> _buildInProgress = new(false);
    private readonly Observable<bool> _isBuildReady = new(false);

    private readonly IResourceService _resourceService;
    private readonly IAssetImporter _assetImporter;
    private readonly IAssetsService _assetsService;
    private readonly IAssetBuilder _assetBuilder;
    private readonly IBuildPreparationService _buildPreparationService;
    private readonly ISourceTracker _sourceTracker;
    private readonly ISolutionBuilder _solutionBuilder;
    private readonly IClientDllManager _clientDllManager;
    private readonly IEditorConsoleService _editorConsoleService;
    private readonly SourceFilesUtility _sourceFilesUtility;
    private readonly ILogger<BuildService> _logger;
    private readonly IEditorProceduresService _editorProceduresService;

    public BuildService(
        IResourceService resourceService,
        IAssetsService assetsService,
        IAssetBuilder assetBuilder,
        IBuildPreparationService buildPreparationService,
        ISourceTracker sourceTracker,
        ISolutionBuilder solutionBuilder,
        IClientDllManager clientDllManager,
        ILogger<BuildService> logger,
        IEditorConsoleService editorConsoleService,
        IAssetImporter assetImporter,
        SourceFilesUtility sourceFilesUtility,
        IEditorProceduresService editorProceduresService)
    {
        _resourceService = resourceService;
        _assetsService = assetsService;
        _assetBuilder = assetBuilder;
        _buildPreparationService = buildPreparationService;
        _sourceTracker = sourceTracker;
        _solutionBuilder = solutionBuilder;
        _clientDllManager = clientDllManager;
        _logger = logger;
        _editorConsoleService = editorConsoleService;
        _assetImporter = assetImporter;
        _sourceFilesUtility = sourceFilesUtility;
        _editorProceduresService = editorProceduresService;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_buildInProgress.Value) return;
        
        _logger.LogWarning("Waiting for build to finish before disposing");
        _discardBuild = true;
        while (_buildInProgress.Value)
        {
            await Task.Delay(10);
        }
    }

    public async Task<bool> BuildProject(
        BuildConfigurationEnum configuration,
        bool forceSolutionRebuild = false,
        bool forceCleanSolutionBuild = false,
        bool forceAssetRebuild = false,
        BuildExecutionContext? buildContext = null,
        bool buildSolution = true,
        bool buildAssets = true,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null,
        CancellationToken cancellationToken = default)
    {
        if (_buildInProgress)
        {
            _logger.LogError("Another build in progress");
            return false;
        }

        Procedure buildProcedure = new(ProcedureTags.BUILD_PROJECT);
        _editorProceduresService.TrackProcedure(buildProcedure);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        var executionContext = buildContext ?? BuildExecutionContext.CreateLive(_resourceService.GetRootPath());
        _discardBuild = false;
        _buildInProgress.Value = true;
        _isBuildReady.Value = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _assetImporter.ReimportAll();
            cancellationToken.ThrowIfCancellationRequested();
            if (!_sourceFilesUtility.AreSourceFilesValid) throw new Exception("Cannot build project with source files validation errors");

            await _buildPreparationService.Prepare(cancellationToken);

            var shouldBuildSolution = forceSolutionRebuild
                || !_clientDllManager.DllExists(executionContext.ClientDllPath)
                || await _sourceTracker.ChangedOrNewSourcesExist();

            if (buildSolution && shouldBuildSolution)
            {
                await _solutionBuilder.Build(configuration, forceCleanSolutionBuild, executionContext.SolutionOutputDirectory, cancellationToken);
            }

            if (buildAssets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _assetBuilder.BuildAssets(executionContext, forceAssetRebuild, onAssetBuilding);
            }
            stopwatch.Stop();

            if (_discardBuild)
            {
                throw new Exception("Build was discarded");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Build canceled.");
            _isBuildReady.Value = false;
            _buildInProgress.Value = false;
            return false;
        }
        catch (Exception e)
        {
            _logger.LogError($"Build Failed in {stopwatch.Elapsed.TotalSeconds:.00} seconds.");
            _logger.LogException(e);
            _isBuildReady.Value = false;
            return false;
        }
        finally
        {
            _buildInProgress.Value = false;
            buildProcedure.Complete();
        }

        _editorConsoleService.ClearConsole();
        _logger.Log($"Build Complete in {stopwatch.Elapsed.TotalSeconds:.00} seconds.");
        _isBuildReady.Value = true;
        return true;
    }
}
