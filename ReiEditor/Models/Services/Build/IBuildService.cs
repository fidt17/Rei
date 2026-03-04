using System.Threading;
using System.Threading.Tasks;
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
        CancellationToken cancellationToken = default);
}
