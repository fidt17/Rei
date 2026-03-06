using System.IO;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.Resources.EngineResources;

public static class ReiAssetIdUtility
{
    public const string Prefix = "rei_";
    public const string ProjectDirectoryName = "Engine Resources";

    public static bool TryCreateFromAssetPath(string assetPath, IResourceService resourceService, out string assetId)
    {
        var engineResourcesRoot = resourceService.GetProjectPath(ProjectDirectoryName);
        return TryCreateFromAssetPath(assetPath, engineResourcesRoot, out assetId);
    }

    public static bool TryCreateFromAssetPath(string assetPath, string engineResourcesRoot, out string assetId)
    {
        assetId = string.Empty;

        if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(engineResourcesRoot)) return false;
        if (!assetPath.IsUnderDirectory(engineResourcesRoot)) return false;

        var normalizedFileName = Path.GetFileName(assetPath).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedFileName)) return false;

        assetId = Prefix + normalizedFileName;
        return true;
    }
}
