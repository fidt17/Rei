using System.IO;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Sync;

public static class RuntimeAssetTypeResolver
{
    public const string MaterialType = "Material";

    public static bool TryResolveAssetType(IAssetRegistry assetRegistry, string assetId, out string assetType)
    {
        assetType = "";
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (!assetRegistry.TryGetById(assetId, out var assetInfo) || assetInfo == null) return false;

        var extension = Path.GetExtension(assetInfo.FullPath);
        if (extension.Equals(FileExtensions.MATERIAL, System.StringComparison.OrdinalIgnoreCase))
        {
            assetType = MaterialType;
            return true;
        }

        return false;
    }
}

