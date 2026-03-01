using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Platform.Storage;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileExtensions
{
    public const string VS_SOLUTION = ".sln";
    public const string VS_PROJECT = ".vcxproj";
    public const string VS_PROJECT_USER = ".vcxproj.user";
    public const string EXE = ".exe";
	
    public const string REI_PROJECT = ".rei";
    public const string REI_ENGINE = ".rei_engine";
	
    public const string SCENE = ".scene";
    public const string ASSET = ".asset";
    public const string META = ".meta";
    public const string CPP = ".cpp";
    public const string H = ".h";
    public const string OBJ = ".obj";
    public const string FBX = ".fbx";
    public const string RSHADER = ".rshader";
    public const string MATERIAL = ".mat";

    public static readonly IReadOnlyList<string> ModelAssetExtensions = new[] { OBJ, FBX };
    public static readonly IReadOnlyList<string> MaterialAssetExtensions = new[] { MATERIAL };
    public static readonly HashSet<string> TextEditorOpenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        H,
        CPP,
        RSHADER
    };

    public static FilePickerFileType GetFilePicker(string fileExtension)
    {
        return new FilePickerFileType(fileExtension)
        {
            Patterns = new[] { $"*{fileExtension}" }
        };
    }
	
    public static HashSet<string> FindAllFilesIn(IEnumerable<string> paths)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    targets.Add(file);
                }
                continue;
            }

            if (File.Exists(path))
            {
                targets.Add(path);
            }
        }

        return targets;
    }

    public static bool IsTextEditorOpenSupported(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        var extension = Path.GetExtension(filePath);
        return TextEditorOpenExtensions.Contains(extension);
    }
}
