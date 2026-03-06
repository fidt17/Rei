using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Build;

public interface IBuildService
{
    IObservable<bool> BuildInProgress { get; }
    IObservable<bool> IsBuildReady { get; }

    Task<bool> BuildProject(
        BuildConfigurationEnum configuration,
        bool forceSolutionRebuild = false,
        bool forceCleanSolutionBuild = false,
        bool forceAssetRebuild = false,
        bool buildSolution = true,
        bool buildAssets = true,
        global::System.Action<AssetBuildProgressInfo>? onAssetBuilding = null,
        CancellationToken cancellationToken = default);
}
