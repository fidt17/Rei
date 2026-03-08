using System.IO;

namespace ReiEditor.Utils;

public static class AssetFileInfoUtility
{
    public static long? TryGetFileSize(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return null;
        }
    }
}
