using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Captures build inputs without invoking native compilation or asset packing.</summary>
internal sealed record TestBuildRequest(BuildConfigurationEnum Configuration, bool ForceSolution, bool CleanSolution, bool ForceAssets,
    BuildExecutionContext? Context, bool BuildSolution, bool BuildAssets, Action<AssetBuildProgressInfo>? Progress, CancellationToken Cancellation);

/// <summary>Controls build state and explicitly configured build results.</summary>
internal sealed class TestBuildService : IBuildService
{
    public Observable<bool> InProgress { get; } = new(false);
    public Observable<bool> Ready { get; } = new(false);
    public ReiEditor.Utils.Common.IObservable<bool> BuildInProgress => InProgress;
    public ReiEditor.Utils.Common.IObservable<bool> IsBuildReady => Ready;
    public List<TestBuildRequest> Requests { get; } = new();
    public Func<TestBuildRequest, Task<bool>>? OnBuild { get; set; }

    public Task<bool> BuildProject(BuildConfigurationEnum configuration, bool forceSolutionRebuild = false, bool forceCleanSolutionBuild = false, bool forceAssetRebuild = false, BuildExecutionContext? buildContext = null, bool buildSolution = true, bool buildAssets = true, Action<AssetBuildProgressInfo>? onAssetBuilding = null, CancellationToken cancellationToken = default)
    {
        var request = new TestBuildRequest(configuration, forceSolutionRebuild, forceCleanSolutionBuild, forceAssetRebuild, buildContext, buildSolution, buildAssets, onAssetBuilding, cancellationToken);
        Requests.Add(request);
        return (OnBuild ?? throw new NotSupportedException())(request);
    }
}
