using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReiEditor.Models.Services.FileSystem;

public static class ProjectSourceFiles
{
    public const string PCH_SOURCE_PATH = @"Internal\ReiProjectPch.cpp";
    public static readonly string[] SOURCE_EXTENSIONS = { FileExtensions.CPP, FileExtensions.H };
    public static readonly string[] HEADER_EXTENSIONS = { FileExtensions.H, ".hpp", ".inl" };

    public static IEnumerable<string> Enumerate(string scriptsPath)
    {
        if (!Directory.Exists(scriptsPath)) return Array.Empty<string>();
        return Directory.EnumerateFiles(scriptsPath, "*", SearchOption.AllDirectories)
            .Where(path => FileExtensions.HasAnyExtension(path, SOURCE_EXTENSIONS))
            .Select(path => Path.GetRelativePath(scriptsPath, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> IncludeRoots(string includes) => includes
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
