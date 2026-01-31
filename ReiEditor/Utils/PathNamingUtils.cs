using System;
using System.IO;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Utils;

public static class PathNamingUtils
{
    public static string GetDuplicatePath(string fullPath, bool isDirectory)
    {
        var parent = Path.GetDirectoryName(fullPath) ?? "";
        string baseName;
        string extension;

        if (isDirectory)
        {
            baseName = Path.GetFileName(fullPath);
            extension = "";
        }
        else
        {
            baseName = Path.GetFileNameWithoutExtension(fullPath);
            extension = Path.GetExtension(fullPath);
        }

        var candidateName = $"{baseName} Copy";
        var candidatePath = Path.Combine(parent, candidateName + extension);
        var counter = 2;

        while (PathExtensions.PathExists(candidatePath, isDirectory))
        {
            candidateName = $"{baseName} Copy {counter}";
            candidatePath = Path.Combine(parent, candidateName + extension);
            counter++;
        }

        return candidatePath;
    }

    public static string GetUniqueDirectoryPath(string parentDirectory, string baseName)
    {
        return GetUniquePath(parentDirectory, baseName, "", Directory.Exists);
    }

    public static string GetUniqueFilePath(string parentDirectory, string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return GetUniquePath(parentDirectory, baseName, extension, File.Exists);
    }

    private static string GetUniquePath(string parentDirectory, string baseName, string extension, Func<string, bool> exists)
    {
        var candidatePath = Path.Combine(parentDirectory, baseName + extension);
        var counter = 2;

        while (exists(candidatePath))
        {
            candidatePath = Path.Combine(parentDirectory, $"{baseName} {counter}{extension}");
            counter++;
        }

        return candidatePath;
    }
}
