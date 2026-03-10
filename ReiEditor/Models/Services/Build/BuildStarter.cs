using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Build;

public class BuildStarter : IBuildStarter, IDisposable
{
    public ICondition CanStartBuild => _canStartBuildCondition;

    private readonly ConditionGroup _canStartBuildCondition;

    private readonly IBuildService _buildService;
    private readonly IEngineRunner _engineRunner;
    private readonly IStagedEditorBuildService _stagedEditorBuildService;
    private readonly IAssetImporter _assetImporter;
    private readonly ILogger<BuildStarter> _logger;

    private int _commandInProgress;

    public BuildStarter(
        IBuildService buildService,
        IAssetsService assetsService,
        IEngineRunner engineRunner,
        IStagedEditorBuildService stagedEditorBuildService,
        IAssetImporter assetImporter,
        ILogger<BuildStarter> logger)
    {
        _buildService = buildService;
        _engineRunner = engineRunner;
        _stagedEditorBuildService = stagedEditorBuildService;
        _assetImporter = assetImporter;
        _logger = logger;

        _canStartBuildCondition = new ConditionGroup(
            new Condition(_buildService.BuildInProgress, target: false),
            new Condition(_engineRunner.IsPlaymodeActive, target: false),
            new Condition(_engineRunner.IsEngineStarting, target: false),
            new Condition(assetsService.SaveInProcess, target: false),
            new Condition(_assetImporter.IsImporting, target: false));
    }

    public void Dispose()
    {
        _canStartBuildCondition.Dispose();
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
        if (Interlocked.Exchange(ref _commandInProgress, 1) == 1) return false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_canStartBuildCondition.IsTrue.Value) return false;

            if (_stagedEditorBuildService.ShouldUseStagedEditorBuild(configuration, buildContext))
            {
                return await _stagedEditorBuildService.BuildAndPromote(
                    configuration,
                    forceSolutionRebuild,
                    forceCleanSolutionBuild,
                    forceAssetRebuild,
                    buildSolution,
                    buildAssets,
                    onAssetBuilding,
                    cancellationToken);
            }

            return await _buildService.BuildProject(
                configuration,
                forceSolutionRebuild,
                forceCleanSolutionBuild,
                forceAssetRebuild,
                buildContext,
                buildSolution,
                buildAssets,
                onAssetBuilding,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _commandInProgress, 0);
        }
    }
}
