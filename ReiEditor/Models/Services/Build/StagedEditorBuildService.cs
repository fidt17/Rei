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
    private readonly IEngineRunner _engineRunner;
    private readonly IEngineBuildGate _engineBuildGate;
    private readonly IEditorBuildOutputService _editorBuildOutputService;

    public StagedEditorBuildService(
        IBuildService buildService,
        IEngineRunner engineRunner,
        IEngineBuildGate engineBuildGate,
        IEditorBuildOutputService editorBuildOutputService)
    {
        _buildService = buildService;
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
        var stagedOutput = _editorBuildOutputService.PrepareStagingOutput();

        try
        {
            var stagedContext = new BuildExecutionContext(
                stagedOutput.BinDirectoryPath,
                stagedOutput.ClientOutputDirectoryPath,
                stagedOutput.ClientDllPath,
                Path.Combine(_editorBuildOutputService.GetLiveOutput().ResourcesDirectoryPath, "Cache"));

            var didBuild = await _buildService.BuildProject(
                configuration,
                forceSolutionRebuild,
                forceCleanSolutionBuild,
                forceAssetRebuild,
                stagedContext,
                buildSolution,
                buildAssets,
                onAssetBuilding,
                cancellationToken);

            if (!didBuild) return false;

            await _engineBuildGate.StopEngineAndWaitForDllUnload(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _editorBuildOutputService.PromoteStagingOutput(stagedOutput);
            return true;
        }
        finally
        {
            _editorBuildOutputService.CleanupStagingOutput();
        }
    }
}
