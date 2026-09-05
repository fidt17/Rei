using System.IO;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Build.Assets;

public static class AssetBuildPathUtility
{
    private static readonly string[] EXCLUDED_FILE_EXTENSIONS =
    {
        FileExtensions.META,
        FileExtensions.H,
        FileExtensions.CPP,
        FileExtensions.VS_PROJECT,
        FileExtensions.VS_SOLUTION
    };

    public static bool ShouldBuildPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var normalizedPath = Path.GetFullPath(path).Replace('/', '\\');
        if (IsInHiddenDirectory(normalizedPath)) return false;
        if (FileExtensions.HasAnyExtension(normalizedPath, EXCLUDED_FILE_EXTENSIONS)) return false;

        return !normalizedPath.EndsWith(FileExtensions.VS_PROJECT_USER, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInHiddenDirectory(string path)
    {
        var currentDirectory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            if (AssetFileFilter.ShouldHideDirectory(currentDirectory)) return true;

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        return false;
    }
}
