using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReiEditor.Utils.Common;

namespace ReiEditor.Utils.Path;

public static class PathNamingUtils
{
    public static string GetRenameValue(string name, bool isDirectory)
    {
        if (isDirectory) return name;
        return System.IO.Path.GetFileNameWithoutExtension(name);
    }

    public static string GetRenamedName(string originalName, string renamedValue, bool isDirectory)
    {
        if (isDirectory) return renamedValue;

        var extension = System.IO.Path.GetExtension(originalName);
        return renamedValue + extension;
    }

    public static string GetUniqueAssetName(string parentDirectory, string baseName, string extension)
    {
        var files = Directory
            .EnumerateFiles(parentDirectory, $"*{extension}")
            .Select(System.IO.Path.GetFileNameWithoutExtension);
        var directories = Directory
            .EnumerateDirectories(parentDirectory)
            .Select(System.IO.Path.GetFileName);
        var existingNames = (IEnumerable<string>) files.Concat(directories);
        
        return NamingUtils.GetUniqueName(baseName, existingNames);
    }
    
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

        IEnumerable<string> existingNames;
        if (isDirectory)
        {
            existingNames = Directory
                .EnumerateDirectories(parent)
                .Select(path => System.IO.Path.GetFileName(path));
        }
        else
        {
            var files = Directory
                .EnumerateFiles(parent, $"*{extension}")
                .Select(path => System.IO.Path.GetFileNameWithoutExtension(path));
            var directories = Directory
                .EnumerateDirectories(parent)
                .Select(path => System.IO.Path.GetFileName(path));
            existingNames = files.Concat(directories);
        }
        var candidateName = NamingUtils.GetDuplicateName(baseName, existingNames);
        var candidatePath = System.IO.Path.Combine(parent, candidateName + extension);

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
