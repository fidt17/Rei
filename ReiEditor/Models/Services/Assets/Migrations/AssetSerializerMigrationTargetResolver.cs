using System;
using System.IO;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets.Migrations;

public static class AssetSerializerMigrationTargetResolver
{
    private const string BUILD_SCENES_CONFIGURATION_FILE_NAME = "Build Scenes Configuration.asset";

    public static bool TryResolveAssetType(string assetPath, out Type assetType)
    {
        assetType = typeof(Asset);

        if (string.IsNullOrWhiteSpace(assetPath)) return false;

        var extension = Path.GetExtension(assetPath);
        if (string.IsNullOrWhiteSpace(extension)) return false;

        if (string.Equals(extension, FileExtensions.SCENE, StringComparison.OrdinalIgnoreCase))
        {
            assetType = typeof(Scene);
            return true;
        }

        if (string.Equals(extension, FileExtensions.MATERIAL, StringComparison.OrdinalIgnoreCase))
        {
            assetType = typeof(Material);
            return true;
        }

        if (string.Equals(extension, FileExtensions.ASSET, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(assetPath);
            if (string.Equals(fileName, BUILD_SCENES_CONFIGURATION_FILE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                assetType = typeof(BuildScenesConfiguration);
                return true;
            }
        }

        return false;
    }
}
