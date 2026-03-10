using System.IO;

namespace ReiEditor.Models.Services.Build.Solution;

public static class MsBuildArgumentsBuilder
{
    public static string Build(string solutionPath, BuildConfigurationEnum configuration, string buildTarget, string? outputDirectory = null)
    {
        var arguments = $"\"{solutionPath}\" -v:q /t:{buildTarget} /p:Configuration={configuration}";
        if (string.IsNullOrWhiteSpace(outputDirectory)) return arguments;

        var normalizedOutputDirectory = Path.GetFullPath(outputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var escapedOutputDirectory = normalizedOutputDirectory + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar;
        return $"{arguments} /p:OutDir=\"{escapedOutputDirectory}\"";
    }
}
