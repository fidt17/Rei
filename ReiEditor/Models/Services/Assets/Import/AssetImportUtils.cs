using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Import;

public static class AssetImportUtils
{
    private static readonly string[] EXCLUDED_META_EXTENSIONS =
    {
        FileExtensions.META,
        FileExtensions.CPP,
        FileExtensions.VS_PROJECT
    };

    public static bool IsValidAssetExtensionForMetaFile(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        
        return !FileExtensions.MatchesAnyExtension(extension, EXCLUDED_META_EXTENSIONS);
    }
}
