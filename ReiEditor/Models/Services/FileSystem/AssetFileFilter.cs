using System;
using System.IO;

namespace ReiEditor.Models.Services.FileSystem;

public static class AssetFileFilter
{
    private static readonly string[] HIDDEN_FILE_EXTENSIONS =
    {
        FileExtensions.META,
        FileExtensions.VS_PROJECT
    };

    private static readonly string[] EXCLUDED_DIRECTORY_SUFFIXES =
    {
        Path.Combine("Project", "Scripts", "bin"),
        Path.Combine("Project", "Scripts", "crash_reports")
    };

    public static bool ShouldHide(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return true;

        var normalizedPath = NormalizePath(filePath);
        if (FileExtensions.HasAnyExtension(normalizedPath, HIDDEN_FILE_EXTENSIONS)) return true;
        if (normalizedPath.EndsWith(FileExtensions.VS_PROJECT_USER, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    public static bool ShouldHideDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return true;

        var normalizedPath = NormalizePath(directoryPath);
        foreach (var excludedSuffix in EXCLUDED_DIRECTORY_SUFFIXES)
        {
            if (normalizedPath.EndsWith(Path.DirectorySeparatorChar + NormalizeRelativePath(excludedSuffix), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
