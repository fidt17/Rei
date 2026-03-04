namespace ReiEditor.Models.Services.Build.ProjectBuild;

public readonly record struct ProjectBuildProgress(
    string Status,
    int CurrentStep,
    int TotalSteps)
{
    public double ProgressValue => TotalSteps <= 0 ? 0 : (double)CurrentStep / TotalSteps;
}
