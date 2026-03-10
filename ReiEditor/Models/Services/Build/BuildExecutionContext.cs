using System.IO;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Build;

public sealed record BuildExecutionContext(
    string BuildFolder,
    string? SolutionOutputDirectory = null,
    string? ClientDllPath = null)
{
    public string ResourcesDirectoryPath => Path.Combine(BuildFolder, ResourceConstants.RESOURCES_DIR_NAME);

    public static BuildExecutionContext CreateLive(string projectRoot)
    {
        return new BuildExecutionContext(Path.Combine(projectRoot, ResourceConstants.BIN_DIR_NAME));
    }
}
