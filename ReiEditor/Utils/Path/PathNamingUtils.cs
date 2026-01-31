using System;
using System.IO;

namespace ReiEditor.Utils.Path;

public static class PathNamingUtils
{
    public static string GetDuplicatePath(string fullPath, bool isDirectory)
    {
        var parent = System.IO.Path.GetDirectoryName(fullPath) ?? "";
        string baseName;
        string extension;

        if (isDirectory)
        {
            baseName = System.IO.Path.GetFileName(fullPath);
            extension = "";
        }
        else
        {
            baseName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
            extension = System.IO.Path.GetExtension(fullPath);
        }

        var candidateName = $"{baseName} Copy";
        var candidatePath = System.IO.Path.Combine(parent, candidateName + extension);
        var counter = 2;

        while (PathExtensions.PathExists(candidatePath, isDirectory))
        {
            candidateName = $"{baseName} Copy {counter}";
            candidatePath = System.IO.Path.Combine(parent, candidateName + extension);
            counter++;
        }

        return candidatePath;
    }

    public static string GetUniqueDirectoryPath(string parentDirectory, string baseName)
    {
        return GetUniquePath(parentDirectory, baseName, "", Directory.Exists, File.Exists);
    }

    public static string GetUniqueFilePath(string parentDirectory, string fileName)
    {
        var baseName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var extension = System.IO.Path.GetExtension(fileName);
        return GetUniquePath(parentDirectory, baseName, extension, File.Exists, Directory.Exists);
    }

    private static string GetUniquePath(
        string parentDirectory,
        string baseName,
        string extension,
        Func<string, bool> primaryExists,
        Func<string, bool> secondaryExists)
    {
        var candidatePath = System.IO.Path.Combine(parentDirectory, baseName + extension);
        var counter = 2;

        while (primaryExists(candidatePath) || secondaryExists(candidatePath))
        {
            candidatePath = System.IO.Path.Combine(parentDirectory, $"{baseName} {counter}{extension}");
            counter++;
        }

        return candidatePath;
    }
}
