using System.IO;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Build.Assets;

public static class AssetBuildPathUtility
{
    public static bool ShouldBuildPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var normalizedPath = Path.GetFullPath(path).Replace('/', '\\');
        if (IsInHiddenDirectory(normalizedPath)) return false;

        var extension = Path.GetExtension(normalizedPath);
        return extension switch
        {
            FileExtensions.META => false,
            FileExtensions.H => false,
            FileExtensions.CPP => false,
            FileExtensions.VS_PROJECT => false,
            FileExtensions.VS_PROJECT_USER => false,
            FileExtensions.VS_SOLUTION => false,
            _ => true
        };
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
