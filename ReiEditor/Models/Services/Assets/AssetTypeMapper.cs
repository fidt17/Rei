using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets;

public sealed class AssetTypeMapper : IAssetTypeMapper
{
    public AssetType GetAssetTypeForTemplateType(string? templateTypeName)
    {
        if (string.IsNullOrWhiteSpace(templateTypeName)) return AssetType.Unknown;

        if (string.Equals(templateTypeName, "Model", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Model;
        }

        if (string.Equals(templateTypeName, "Material", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Material;
        }

        return AssetType.Unknown;
    }

    public IReadOnlyList<string> GetExtensionsForAssetType(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Model => FileExtensions.ModelAssetExtensions,
            AssetType.Material => FileExtensions.MaterialAssetExtensions,
            _ => Array.Empty<string>()
        };
    }
}
