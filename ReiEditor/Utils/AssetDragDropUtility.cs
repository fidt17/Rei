using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace ReiEditor.Utils;

public static class AssetDragDropUtility
{
    public static List<string> GetAssetPaths(IDataObject data)
    {
        if (data.Contains(DragDropDataKeys.AssetPaths) && data.Get(DragDropDataKeys.AssetPaths) is IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (data.Contains(DragDropDataKeys.AssetPath) && data.Get(DragDropDataKeys.AssetPath) is string assetPath && !string.IsNullOrWhiteSpace(assetPath))
        {
            return new List<string> { assetPath };
        }

        return new List<string>();
    }
}
