using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Rei.EngineIntegration.Tests;

internal static class ExternalProjectCopy
{
    private static readonly HashSet<string> XML_BUILD_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vcxproj", ".props", ".targets"
    };
    private static readonly HashSet<string> LOCALIZED_EXTENSIONS = new(XML_BUILD_EXTENSIONS, StringComparer.OrdinalIgnoreCase)
    {
        ".sln", ".h", ".hpp", ".hxx", ".cpp", ".cc", ".cxx", ".c", ".inl"
    };

    internal static void ValidateSource(string source)
    {
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException($"Source project does not exist: {source}");
        ValidateParents(source);
        ValidateTree(source);
        if (Directory.GetFiles(source, "*.rei").Length != 1)
            throw new InvalidDataException("Source directory must contain exactly one .rei project.");
    }

    internal static void ValidateDestination(string source, string destination)
    {
        ValidateParents(destination);
        var sourcePrefix = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destinationPrefix = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destinationPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) ||
            sourcePrefix.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Source and owned project directories must not overlap.");
    }

    private static void ValidateParents(string path)
    {
        for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
            if (directory.Exists) RejectLink(directory.FullName);
    }

    private static void ValidateTree(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            RejectLink(entry);
            if (Directory.Exists(entry)) ValidateTree(entry);
        }
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Source project must not contain or traverse links: {path}");
    }

    internal static string RelativeProjectPath(string source, string path)
    {
        var full = Path.GetFullPath(Path.Combine(source, path));
        if (!full.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Project path escapes source directory: {path}");
        return Path.GetRelativePath(source, full);
    }

    internal static async Task LocalizeBuildFilesAsync(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
            .Where(file => LOCALIZED_EXTENSIONS.Contains(Path.GetExtension(file))))
        {
            var text = await File.ReadAllTextAsync(file);
            foreach (var separator in new[] { '\\', '/' })
            {
                var from = source.Replace('\\', separator).Replace('/', separator);
                var to = destination.Replace('\\', separator).Replace('/', separator);
                text = Regex.Replace(text, Regex.Escape(from) + "(?=[\\\\/\"<;\\s]|$)", _ => to, RegexOptions.IgnoreCase);
            }
            if (XML_BUILD_EXTENSIONS.Contains(Path.GetExtension(file)))
                ValidateOutputPaths(text, file, destination);
            await File.WriteAllTextAsync(file, text);
        }
        // Copied tracking files contain original absolute outputs. Never let Clean/Build consume them.
        var bin = Path.Combine(destination, "bin");
        if (Directory.Exists(bin))
        {
            foreach (var file in Directory.EnumerateFiles(bin, "*", SearchOption.AllDirectories).Where(IsBuildTrackingFile))
                File.Delete(file);
            foreach (var file in Directory.EnumerateFiles(bin, "asset-cache.json", SearchOption.AllDirectories))
            {
                // Cached payloads are keyed by asset ID and content. Remap only the
                // manifest's source paths so a copied cache remains valid in its owned project.
                var manifest = JsonNode.Parse(await File.ReadAllTextAsync(file))!;
                if (manifest["Entries"] is not JsonObject entries) continue;
                var prefix = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var entry in entries.Select(entry => entry.Value).OfType<JsonObject>())
                {
                    var path = entry["AssetPath"]?.GetValue<string>();
                    if (path == null || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    entry["AssetPath"] = Path.Combine(destination, RelativeProjectPath(source, path));
                }
                await File.WriteAllTextAsync(file, manifest.ToJsonString());
            }
        }
    }

    private static bool IsBuildTrackingFile(string file) =>
        Path.GetExtension(file).Equals(".tlog", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(file).Equals(".lastbuildstate", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".FileListAbsolute.txt", StringComparison.OrdinalIgnoreCase);

    private static void ValidateOutputPaths(string text, string projectFile, string destination)
    {
        var document = XDocument.Parse(text);
        if (Path.GetExtension(projectFile).Equals(".vcxproj", StringComparison.OrdinalIgnoreCase))
            foreach (var required in new[] { "OutDir", "IntDir" })
                if (!document.Descendants().Any(element => element.Name.LocalName == required))
                    throw new InvalidDataException($"External project must declare {required} so isolation can be verified.");
        foreach (var element in document.Descendants().Where(element => element.Name.LocalName is "OutDir" or "IntDir"))
        {
            var path = element.Value.Trim()
                .Replace("$(SolutionDir)", destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                .Replace("$(ProjectDir)", Path.GetDirectoryName(projectFile) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                .Replace("$(MSBuildProjectDirectory)", Path.GetDirectoryName(projectFile), StringComparison.OrdinalIgnoreCase)
                .Replace("$(Platform)", "x64", StringComparison.OrdinalIgnoreCase)
                .Replace("$(Configuration)", "EditorDebug", StringComparison.OrdinalIgnoreCase)
                .Replace("$(ProjectName)", Path.GetFileNameWithoutExtension(projectFile), StringComparison.OrdinalIgnoreCase);
            if (path.Contains("$(", StringComparison.Ordinal))
                throw new InvalidDataException($"Cannot verify isolated {element.Name.LocalName}: {element.Value}");
            var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, path));
            if (!full.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Build output escapes isolated project: {element.Value}");
        }
    }

    internal static async Task<SortedDictionary<string, string>> FingerprintAsync(string source)
    {
        ValidateSource(source);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(file);
            result.Add(Path.GetRelativePath(source, file), Convert.ToHexString(await SHA256.HashDataAsync(stream)));
        }
        return result;
    }
}
