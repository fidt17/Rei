using System.Collections.Generic;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetTypeMapper
{
    AssetType GetAssetTypeForTemplateType(string? templateTypeName);
    IReadOnlyList<string> GetExtensionsForAssetType(AssetType assetType);
}
