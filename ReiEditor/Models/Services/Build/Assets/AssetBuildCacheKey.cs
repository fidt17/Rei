using System;

namespace ReiEditor.Models.Services.Build.Assets;

public static class AssetBuildCacheKey
{
    public static string Create(string engineVersion)
    {
        if (engineVersion == null) throw new ArgumentNullException(nameof(engineVersion));

        return $"{engineVersion}|{AssetBuildVersions.BUILDER_VERSION}";
    }
}