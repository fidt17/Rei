namespace ReiEditor.Models.Services.Build.ProjectBuild;

public readonly record struct ProjectBuildRequest(
    BuildConfigurationEnum Configuration,
    string OutputPath,
    bool ShowConsole,
    string IconPath);
