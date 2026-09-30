using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReiEditor.Models.Services.FileSystem;
using IOPath = System.IO.Path;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public static class ProjectAssetIconProvider
{
    private static readonly Dictionary<string, IImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IImage GetAssetIcon(ProjectAssetType assetType, string fullPath)
    {
        var iconName = GetIconName(assetType, fullPath);
        return GetIcon($"avares://ReiEditor/Assets/Icons/Lucide/{iconName}.png");
    }

    private static string GetIconName(ProjectAssetType assetType, string fullPath)
    {
        if (assetType == ProjectAssetType.Directory) return AssetFileFilter.HasVisibleContents(fullPath) ? "folder-closed" : "folder";

        var extension = IOPath.GetExtension(fullPath);
        if (string.Equals(extension, FileExtensions.SCENE, StringComparison.OrdinalIgnoreCase)) return "globe";
        if (FileExtensions.HasAnyExtension(fullPath, FileExtensions.ModelAssetExtensions)) return "box";
        if (FileExtensions.HasAnyExtension(fullPath, FileExtensions.TextureAssetExtensions)) return "image";
        if (FileExtensions.HasAnyExtension(fullPath, FileExtensions.MaterialAssetExtensions)) return "contrast";
        if (FileExtensions.HasAnyExtension(fullPath, FileExtensions.FontAssetExtensions)) return "type";
        if (FileExtensions.IsTextEditorOpenSupported(fullPath)) return "file-braces-corner";
        if (string.Equals(extension, FileExtensions.ASSET, StringComparison.OrdinalIgnoreCase)) return "file-sliders";

        return "file";
    }

    private static IImage GetIcon(string uri)
    {
        if (Cache.TryGetValue(uri, out var cached)) return cached;
        using var stream = AssetLoader.Open(new Uri(uri));
        var bitmap = new Bitmap(stream);
        Cache[uri] = bitmap;
        return bitmap;
    }
}
