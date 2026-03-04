namespace ReiEditor.Models.Services.Build.ProjectBuild;

public readonly record struct ProjectBuildResult(
    bool IsSuccess,
    bool IsCancelled,
    string ErrorMessage);
