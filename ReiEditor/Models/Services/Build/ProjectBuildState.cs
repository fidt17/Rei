namespace ReiEditor.Models.Services.Build;

public sealed record ProjectBuildState(
    bool ShouldBuildSolution,
    bool ShouldBuildAssets,
    string Reason);
