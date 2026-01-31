using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public static class ProjectAssetIconProvider
{
    private static readonly Dictionary<string, IImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IImage GetAssetIcon(ProjectAssetType assetType)
    {
        return assetType switch
        {
            ProjectAssetType.Directory => GetIcon("avares://ReiEditor/Assets/Images/project_folder.png"),
            ProjectAssetType.Scene => GetIcon("avares://ReiEditor/Assets/Images/project_scene.png"),
            ProjectAssetType.Script => GetIcon("avares://ReiEditor/Assets/Images/project_script.png"),
            
            _ => GetIcon("avares://ReiEditor/Assets/Images/project_asset.png")
        };
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
