using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build.Solution;

public interface ISolutionBuilder
{
    Task Build(
        BuildConfigurationEnum configuration,
        bool cleanBuild = false,
        CancellationToken cancellationToken = default);
}
