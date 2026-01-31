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
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase)) return false;
        if (fullPath.Length == fullDirectory.Length) return true;

        var nextChar = fullPath[fullDirectory.Length];
        return nextChar == Path.DirectorySeparatorChar || nextChar == Path.AltDirectorySeparatorChar;
    }
    
    public static bool PathExists(string path, bool isDirectory)
    {
        return isDirectory ? Directory.Exists(path) : File.Exists(path);
    }
}
