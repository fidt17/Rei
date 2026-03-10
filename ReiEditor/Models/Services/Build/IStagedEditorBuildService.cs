using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build.Assets;

namespace ReiEditor.Models.Services.Build;

public interface IStagedEditorBuildService
{
    bool ShouldUseStagedEditorBuild(BuildConfigurationEnum configuration, BuildExecutionContext? context);

    Task<bool> BuildAndPromote(
        BuildConfigurationEnum configuration,
        bool forceSolutionRebuild,
        bool forceCleanSolutionBuild,
        bool forceAssetRebuild,
        bool buildSolution,
        bool buildAssets,
        Action<AssetBuildProgressInfo>? onAssetBuilding,
        CancellationToken cancellationToken);
}
