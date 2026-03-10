using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Build;

public interface IProjectBuildStateService
{
    Task<ProjectBuildState> CalculateState(
        BuildConfigurationEnum configuration,
        BuildExecutionContext buildContext,
        bool buildSolution,
        bool buildAssets);

    void MarkBuildStarted(BuildConfigurationEnum configuration, BuildExecutionContext buildContext);
    void MarkBuildFailed(BuildConfigurationEnum configuration, BuildExecutionContext buildContext);

    Task SaveSuccessfulBuild(
        BuildConfigurationEnum configuration,
        BuildExecutionContext buildContext,
        bool buildSolution,
        bool buildAssets);
}
