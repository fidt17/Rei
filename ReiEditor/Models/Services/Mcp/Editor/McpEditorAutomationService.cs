using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Capture;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal sealed class McpEditorAutomationService : IMcpEditorAutomationService
{
    private const int MAX_LOG_LIMIT = 500;

    private readonly IAssetsService _assetsService;
    private readonly ISceneStateSynchronizer _sceneStateSynchronizer;
    private readonly IEngineRunner _engineRunner;
    private readonly IBuildService _buildService;
    private readonly IBuildStarter _buildStarter;
    private readonly IAssetImporter _assetImporter;
    private readonly IPlaymodeStartWorkflow _playmodeStartWorkflow;
    private readonly IEditorConsoleService _editorConsoleService;
    private readonly IMcpEditorOperationCoordinator _operationCoordinator;
    private readonly IEngineFrameCaptureService _frameCaptureService;

    public McpEditorAutomationService(
        IAssetsService assetsService,
        ISceneStateSynchronizer sceneStateSynchronizer,
        IEngineRunner engineRunner,
        IBuildService buildService,
        IBuildStarter buildStarter,
        IAssetImporter assetImporter,
        IPlaymodeStartWorkflow playmodeStartWorkflow,
        IEditorConsoleService editorConsoleService,
        IMcpEditorOperationCoordinator operationCoordinator,
        IEngineFrameCaptureService frameCaptureService)
    {
        _assetsService = assetsService;
        _sceneStateSynchronizer = sceneStateSynchronizer;
        _engineRunner = engineRunner;
        _buildService = buildService;
        _buildStarter = buildStarter;
        _assetImporter = assetImporter;
        _playmodeStartWorkflow = playmodeStartWorkflow;
        _editorConsoleService = editorConsoleService;
        _operationCoordinator = operationCoordinator;
        _frameCaptureService = frameCaptureService;
    }

    public ReiAutomationState GetState()
    {
        return new ReiAutomationState(
            _assetImporter.IsImporting.Value,
            _buildService.BuildInProgress.Value,
            _operationCoordinator.GetActiveOperation());
    }

    public ReiEngineInfo GetEngineInfo()
    {
        if (_engineRunner.IsEngineStarting.Value) return new ReiEngineInfo("starting", _engineRunner.ActiveMode.ToString());
        if (_engineRunner.IsActive.Value) return new ReiEngineInfo("running", _engineRunner.ActiveMode.ToString());
        return new ReiEngineInfo("stopped", null);
    }

    public async Task<ReiProjectSaveResult> SaveProjectAsync()
    {
        if (_operationCoordinator.GetActiveOperation() is { } activeOperation)
        {
            throw new ReiMcpOperationException("save_unavailable", $"Project cannot be saved during operation {activeOperation.Id} ({activeOperation.Kind}).");
        }

        if (_engineRunner.IsPlaymodeActive.Value)
        {
            throw new ReiMcpOperationException("save_unavailable", "Project cannot be saved while play mode is active.");
        }

        if (_buildService.BuildInProgress.Value)
        {
            throw new ReiMcpOperationException("save_unavailable", "Project cannot be saved while a build is running.");
        }

        if (_assetsService.SaveInProcess.Value)
        {
            throw new ReiMcpOperationException("save_in_progress", "Another project save is already running.");
        }

        _sceneStateSynchronizer.SynchronizeStateWithEngine();
        await _assetsService.SaveProject();

        return new ReiProjectSaveResult(true, DateTimeOffset.UtcNow, "Project saved.");
    }

    public ReiOperationInfo StartAssetRefresh()
    {
        if (_engineRunner.IsPlaymodeActive.Value || _engineRunner.IsEngineStarting.Value)
        {
            throw new ReiMcpOperationException("refresh_unavailable", "Assets cannot be refreshed while play mode is active or engine is starting.");
        }

        if (_buildService.BuildInProgress.Value || _assetsService.SaveInProcess.Value || _assetImporter.IsImporting.Value)
        {
            throw new ReiMcpOperationException("refresh_unavailable", "Assets cannot be refreshed while build, save, or another import is running.");
        }

        return _operationCoordinator.Start(ReiOperationKinds.REFRESH_ASSETS, async context =>
        {
            context.Report(0.05, "Refreshing project assets.");
            var assets = await _assetImporter.ReimportAll();
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.HasErrors)
            {
                throw new ReiMcpOperationException("refresh_failed", "Asset refresh logged errors. Inspect operation logs.");
            }

            context.Report(1, $"Refreshed {assets.Count} assets.");
            return $"Asset refresh complete. {assets.Count} assets registered.";
        });
    }

    public ReiOperationInfo StartBuild(ReiBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuration = ParseBuildConfiguration(options.Configuration);
        if (!options.BuildSolution && !options.BuildAssets)
        {
            throw new ReiMcpOperationException("invalid_build_options", "Build must include solution, assets, or both.");
        }

        if (options.ForceCleanSolutionBuild && !options.BuildSolution)
        {
            throw new ReiMcpOperationException("invalid_build_options", "Solution clean requires solution build.");
        }

        if (!_buildStarter.CanStartBuild.IsTrue.Value)
        {
            throw new ReiMcpOperationException("build_unavailable", "Build cannot start during play mode, engine startup, save, import, or another build.");
        }

        return _operationCoordinator.Start(ReiOperationKinds.BUILD_PROJECT, async context =>
        {
            context.Report(0.02, $"Starting {options.Configuration} build.");
            var didBuild = await _buildStarter.BuildProject(
                configuration,
                options.ForceSolutionRebuild || options.ForceCleanSolutionBuild,
                options.ForceCleanSolutionBuild,
                options.ForceAssetRebuild,
                buildSolution: options.BuildSolution,
                buildAssets: options.BuildAssets,
                onAssetBuilding: progress =>
                {
                    var fraction = progress.TotalAssets == 0 ? 0 : (double) progress.CurrentAssetIndex / progress.TotalAssets;
                    context.Report(0.5 + fraction * 0.45, $"Building asset {progress.CurrentAssetIndex}/{progress.TotalAssets}: {progress.AssetPath}");
                },
                cancellationToken: context.CancellationToken);

            context.CancellationToken.ThrowIfCancellationRequested();
            if (!didBuild)
            {
                throw new ReiMcpOperationException("build_failed", "Project build failed. Inspect operation logs.");
            }

            context.Report(1, "Project build complete.");
            return $"{options.Configuration} project build complete.";
        });
    }

    public ReiOperationInfo StartPlaymode()
    {
        return _operationCoordinator.Start(ReiOperationKinds.START_PLAYMODE, async context =>
        {
            if (_engineRunner.IsPlaymodeActive.Value) return "Play mode already active.";

            context.Report(0.05, "Saving project and preparing EditorDebug build.");
            var didStart = await _playmodeStartWorkflow.StartAsync(context.CancellationToken);
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!didStart)
            {
                throw new ReiMcpOperationException("playmode_start_failed", "Play mode could not start. Inspect operation logs.");
            }

            context.Report(1, "Play mode started.");
            return "Play mode started.";
        });
    }

    public ReiOperationInfo StopPlaymode()
    {
        return _operationCoordinator.Start(ReiOperationKinds.STOP_PLAYMODE, async context =>
        {
            if (!_engineRunner.IsPlaymodeActive.Value) return "Play mode already stopped.";

            context.Report(0.25, "Stopping play mode.");
            await _engineRunner.StopEngine();
            context.CancellationToken.ThrowIfCancellationRequested();
            context.Report(1, "Play mode stopped.");
            return "Play mode stopped.";
        });
    }

    public ReiOperationInfo GetOperation(string operationId) => _operationCoordinator.Get(operationId);

    public ReiOperationInfo CancelOperation(string operationId) => _operationCoordinator.Cancel(operationId);

    public ReiLogList GetLogs(string? operationId, string minimumLevel, int limit)
    {
        if (limit is < 1 or > MAX_LOG_LIMIT)
        {
            throw new ReiMcpOperationException("invalid_log_limit", $"Log limit must be between 1 and {MAX_LOG_LIMIT}.");
        }

        var minimumRank = McpEditorLogUtility.ParseMinimumLevel(minimumLevel);
        var allEntries = operationId == null
            ? _editorConsoleService.Logs.Select(McpEditorLogUtility.CreateEntry).ToList()
            : _operationCoordinator.GetLogs(operationId).ToList();
        var filteredEntries = allEntries
            .Where(x => McpEditorLogUtility.GetLevelRank(x.Level) >= minimumRank)
            .ToList();
        var entries = filteredEntries
            .Skip(Math.Max(0, filteredEntries.Count - limit))
            .ToList();

        return new ReiLogList(
            operationId?.Trim(),
            filteredEntries.Count,
            entries.Count < filteredEntries.Count,
            entries);
    }

    public async Task<ReiFrameCapture> CaptureFrameAsync(CancellationToken cancellationToken)
    {
        EngineFrameCaptureResult capture;
        try
        {
            capture = await _frameCaptureService.CaptureAsync(cancellationToken);
        }
        catch (EngineFrameCaptureException exception)
        {
            throw new ReiMcpOperationException("capture_" + exception.Code, exception.Message);
        }

        return new ReiFrameCapture(
            capture.PngData,
            capture.Width,
            capture.Height,
            DateTimeOffset.UtcNow,
            _engineRunner.ActiveMode.ToString());
    }

    private static BuildConfigurationEnum ParseBuildConfiguration(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new ReiMcpOperationException("invalid_build_configuration", "Build configuration must not be empty.");
        }

        return configuration.Trim().ToLowerInvariant() switch
        {
            ReiBuildConfigurations.DEBUG => BuildConfigurationEnum.Debug,
            ReiBuildConfigurations.EDITOR_DEBUG => BuildConfigurationEnum.EditorDebug,
            ReiBuildConfigurations.RELEASE => BuildConfigurationEnum.Release,
            ReiBuildConfigurations.EDITOR_RELEASE => BuildConfigurationEnum.EditorRelease,
            _ => throw new ReiMcpOperationException(
                "invalid_build_configuration",
                $"Unknown build configuration {configuration}. Expected debug, editor_debug, release, or editor_release.")
        };
    }
}
