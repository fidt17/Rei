using System;
using System.IO;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets;

public static class AssetMonitorSupportUtility
{
    public static bool IsInteractiveAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        var extension = Path.GetExtension(fullPath);
        return string.Equals(extension, FileExtensions.MATERIAL, StringComparison.OrdinalIgnoreCase);
    }
}
