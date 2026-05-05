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

        if (string.Equals(templateTypeName, "Texture", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Texture;
        }

        if (string.Equals(templateTypeName, "Font", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateTypeName, "render::Font", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateTypeName, "rei::render::Font", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Font;
        }

        return AssetType.Unknown;
    }

    public IReadOnlyList<string> GetExtensionsForAssetType(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Model => FileExtensions.ModelAssetExtensions,
            AssetType.Material => FileExtensions.MaterialAssetExtensions,
            AssetType.Texture => FileExtensions.TextureAssetExtensions,
            AssetType.Font => FileExtensions.FontAssetExtensions,
            _ => Array.Empty<string>()
        };
    }
}
