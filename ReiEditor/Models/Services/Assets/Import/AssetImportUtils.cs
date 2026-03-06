using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Import;

public static class AssetImportUtils
{
    public static bool IsValidAssetExtensionForMetaFile(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        
        return extension is not (
            FileExtensions.META or 
            FileExtensions.CPP or 
            FileExtensions.VS_PROJECT);
    }
}
