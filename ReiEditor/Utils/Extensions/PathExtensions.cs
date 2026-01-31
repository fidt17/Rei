using System;
using System.IO;

namespace ReiEditor.Utils.Extensions;

public static class PathExtensions
{
    public static string ToFullPath(this string path)
    {
        return Path.GetFullPath(path);
    }

    public static bool PathEquals(this string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnderDirectory(this string path, string directory)
    {
        return Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
    }
    
    public static bool PathExists(string path, bool isDirectory)
    {
        return isDirectory ? Directory.Exists(path) : File.Exists(path);
    }
}
