using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets;

public sealed class AssetTypeMapper : IAssetTypeMapper
{
    public AssetType GetAssetTypeForTemplateType(string? templateTypeName)
    {
        if (string.IsNullOrWhiteSpace(templateTypeName)) return AssetType.Unknown;

        return string.Equals(templateTypeName, "Model", StringComparison.OrdinalIgnoreCase)
            ? AssetType.Model
            : AssetType.Unknown;
    }

    public IReadOnlyList<string> GetExtensionsForAssetType(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Model => FileExtensions.ModelAssetExtensions,
            _ => Array.Empty<string>()
        };
    }
}
