using System;
using System.Collections.Generic;
using System.IO;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets;

public static class AssetMonitorSupportUtility
{
    private static readonly IReadOnlyCollection<string> InteractiveAssetExtensions = new[]
    {
        FileExtensions.MATERIAL,
    };

    private static readonly IReadOnlyCollection<string> TextPreviewAssetExtensions = new[]
    {
        FileExtensions.H,
        FileExtensions.CPP,
        FileExtensions.RSHADER,
    };

    public static bool IsInteractiveAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return HasSupportedExtension(fullPath, InteractiveAssetExtensions);
    }

    public static bool IsTextPreviewAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return HasSupportedExtension(fullPath, TextPreviewAssetExtensions);
    }

    private static bool HasSupportedExtension(string fullPath, IReadOnlyCollection<string> supportedExtensions)
    {
        var extension = Path.GetExtension(fullPath);
        foreach (var supportedExtension in supportedExtensions)
        {
            if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
