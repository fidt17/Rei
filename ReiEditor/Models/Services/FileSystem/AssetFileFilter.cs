using System;
using System.IO;

namespace ReiEditor.Models.Services.FileSystem;

public static class AssetFileFilter
{
    public static bool ShouldHide(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return true;

        var extension = Path.GetExtension(filePath);
        if (extension == FileExtensions.META) return true;
        if (extension == FileExtensions.VS_PROJECT) return true;
        if (filePath.EndsWith(FileExtensions.VS_PROJECT_USER, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}
