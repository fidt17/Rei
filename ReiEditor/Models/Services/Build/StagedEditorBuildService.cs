using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Build;

public class StagedEditorBuildService : IStagedEditorBuildService
{
    private readonly IBuildService _buildService;
    private readonly IProjectBuildStateService _projectBuildStateService;
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineBuildGate _engineBuildGate;
    private readonly IEditorBuildOutputService _editorBuildOutputService;

    public StagedEditorBuildService(
        IBuildService buildService,
        IProjectBuildStateService projectBuildStateService,
        IEngineRunner engineRunner,
        IEngineBuildGate engineBuildGate,
        IEditorBuildOutputService editorBuildOutputService)
    {
        _buildService = buildService;
        _projectBuildStateService = projectBuildStateService;
        _engineRunner = engineRunner;
        _engineBuildGate = engineBuildGate;
        _editorBuildOutputService = editorBuildOutputService;
    }

    public bool ShouldUseStagedEditorBuild(BuildConfigurationEnum configuration, BuildExecutionContext? context)
    {
        if (configuration != BuildConfigurationEnum.EditorDebug) return false;
        if (context != null) return false;
        if (_engineRunner.IsPlaymodeActive.Value) return false;
        return _engineRunner.IsEditorActive.Value;
    }

    public async Task<bool> BuildAndPromote(
        BuildConfigurationEnum configuration,
        bool forceSolutionRebuild,
        bool forceCleanSolutionBuild,
        bool forceAssetRebuild,
        bool buildSolution,
        bool buildAssets,
        Action<AssetBuildProgressInfo>? onAssetBuilding,
        CancellationToken cancellationToken)
    {
        var liveOutput = _editorBuildOutputService.GetLiveOutput();
        var liveContext = new BuildExecutionContext(
            liveOutput.BinDirectoryPath,
            liveOutput.ClientOutputDirectoryPath,
            liveOutput.ClientDllPath,
            Path.Combine(liveOutput.ResourcesDirectoryPath, "Cache"));

        var buildStateEvaluation = await _projectBuildStateService.CalculateState(configuration, liveContext, buildSolution, buildAssets);
        var shouldBuildSolution = buildSolution && (forceSolutionRebuild || buildStateEvaluation.ShouldBuildSolution);
        var shouldBuildAssets = buildAssets && (forceAssetRebuild || buildStateEvaluation.ShouldBuildAssets);
        if (!shouldBuildSolution && !shouldBuildAssets)
        {
            return true;
        }

        var stagedOutput = _editorBuildOutputService.PrepareStagingOutput();

        try
        {
            var stagedContext = new BuildExecutionContext(
                stagedOutput.BinDirectoryPath,
                stagedOutput.ClientOutputDirectoryPath,
                stagedOutput.ClientDllPath,
                Path.Combine(liveOutput.ResourcesDirectoryPath, "Cache"));

            _projectBuildStateService.MarkBuildStarted(configuration, liveContext);

            var didBuild = await _buildService.BuildProject(
                configuration,
                forceSolutionRebuild: true,
                forceCleanSolutionBuild,
                forceAssetRebuild: true,
                stagedContext,
                shouldBuildSolution,
                shouldBuildAssets,
                onAssetBuilding,
                cancellationToken);

            if (!didBuild)
            {
                _projectBuildStateService.MarkBuildFailed(configuration, liveContext);
                return false;
            }

            await _engineBuildGate.StopEngineAndWaitForDllUnload(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _editorBuildOutputService.PromoteStagingOutput(stagedOutput);
            await _projectBuildStateService.SaveSuccessfulBuild(configuration, liveContext, shouldBuildSolution, shouldBuildAssets);
            return true;
        }
        catch
        {
            _projectBuildStateService.MarkBuildFailed(configuration, liveContext);
            throw;
        }
        finally
        {
            _editorBuildOutputService.CleanupStagingOutput();
        }
    }
}
