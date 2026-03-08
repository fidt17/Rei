using System;
using System.Collections.Generic;
using System.IO;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets;

public static class AssetMonitorSupportUtility
{
    private static readonly IReadOnlyCollection<string> TextPreviewAssetExtensions = new[]
    {
        FileExtensions.H,
        FileExtensions.CPP,
        FileExtensions.RSHADER,
    };

    public static bool IsAssetSupportedInMonitor(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return IsMaterialAsset(fullPath, isDirectory) || IsTexturePreviewAsset(fullPath, isDirectory);
    }

    public static bool IsMaterialAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return FileExtensions.HasAnyExtension(fullPath, FileExtensions.MaterialAssetExtensions);
    }

    public static bool IsTexturePreviewAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return FileExtensions.HasAnyExtension(fullPath, FileExtensions.TextureAssetExtensions);
    }

    public static bool IsTextPreviewAsset(string fullPath, bool isDirectory)
    {
        if (isDirectory) return false;
        if (string.IsNullOrWhiteSpace(fullPath)) return false;

        return FileExtensions.HasAnyExtension(fullPath, TextPreviewAssetExtensions);
    }
}
