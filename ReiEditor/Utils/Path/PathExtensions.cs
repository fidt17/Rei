using System;
using System.IO;

namespace ReiEditor.Utils.Path;

public static class PathExtensions
{
    public static string ToFullPath(this string path)
    {
        return System.IO.Path.GetFullPath(path);
    }

    public static bool PathEquals(this string left, string right)
    {
        return string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnderDirectory(this string path, string directory)
    {
        var fullPath = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var fullDirectory = System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

        if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase)) return false;
        if (fullPath.Length == fullDirectory.Length) return true;

        var nextChar = fullPath[fullDirectory.Length];
        return nextChar == System.IO.Path.DirectorySeparatorChar || nextChar == System.IO.Path.AltDirectorySeparatorChar;
    }
    
    public static bool PathExists(string path, bool isDirectory)
    {
        return isDirectory ? Directory.Exists(path) : File.Exists(path);
    }
}
