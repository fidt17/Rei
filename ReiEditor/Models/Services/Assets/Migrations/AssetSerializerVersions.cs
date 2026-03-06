using System;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Assets.Migrations;

public static class AssetSerializerVersions
{
    public const int LEGACY_VERSION = 0;
    public const int SCENE_VERSION = 1;
    public const int MATERIAL_VERSION = 1;
    public const int SHADER_VERSION = 1;
    public const int BUILD_SCENES_CONFIGURATION_VERSION = 1;

    public static bool TryGetCurrentVersion(Type assetType, out int version)
    {
        if (assetType == typeof(Scene))
        {
            version = SCENE_VERSION;
            return true;
        }

        if (assetType == typeof(Material))
        {
            version = MATERIAL_VERSION;
            return true;
        }

        if (assetType == typeof(Shader))
        {
            version = SHADER_VERSION;
            return true;
        }

        if (assetType == typeof(BuildScenesConfiguration))
        {
            version = BUILD_SCENES_CONFIGURATION_VERSION;
            return true;
        }

        version = LEGACY_VERSION;
        return false;
    }

    public static int GetCurrentVersion(Type assetType)
    {
        return TryGetCurrentVersion(assetType, out var version) ? version : LEGACY_VERSION;
    }
}
