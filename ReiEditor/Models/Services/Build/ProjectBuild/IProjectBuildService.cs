using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build.ProjectBuild;

public interface IProjectBuildService
{
    Task<ProjectBuildResult> BuildAsync(
        ProjectBuildRequest request,
        Action<ProjectBuildProgress> progressCallback,
        CancellationToken cancellationToken);
}
