using System;
using System.IO;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.FileSystem;

public static class FileContentUtility
{
    public static async Task WriteIfChanged(string path, string contents)
    {
        if (File.Exists(path) && string.Equals(await File.ReadAllTextAsync(path), contents, StringComparison.Ordinal)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, contents);
    }
}
