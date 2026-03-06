using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Models.Services.Build;

public interface IBuildStarter
{
    ICondition CanStartBuild { get; }

    Task<bool> BuildProject(
        BuildConfigurationEnum configurationEnum,
        bool forceSolutionRebuild = false,
        bool forceCleanSolutionBuild = false,
        bool forceAssetRebuild = false,
        bool buildSolution = true,
        bool buildAssets = true,
        Action<AssetBuildProgressInfo>? onAssetBuilding = null,
        CancellationToken cancellationToken = default);
}
