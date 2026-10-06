using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rei.EngineIntegration.Tests;

internal static class PreparedBenchmarkProject
{
    private sealed record Entry(string Source, string RunDirectory, SortedDictionary<string, string> Hashes);

    internal static async Task<SortedDictionary<string, string>> FingerprintInputsAsync(string source)
    {
        ExternalProjectCopy.ValidateSource(source);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            byte[] content;
            if (Path.GetExtension(file).Equals(".rei", StringComparison.OrdinalIgnoreCase))
            {
                var project = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject();
                project.Remove("LastEditTime"); // Bookkeeping timestamp is not a build or scene input.
                content = Encoding.UTF8.GetBytes(project.ToJsonString());
            }
            else content = await File.ReadAllBytesAsync(file);
            result.Add(relative, Convert.ToHexString(SHA256.HashData(content)));
        }
        return result;
    }

    internal static async Task<EngineIntegrationHarness> OpenAsync(string source, TimeSpan? startupTimeout = null, string? indexDirectory = null)
    {
        source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar);
        ExternalProjectCopy.ValidateSource(source);
        var hashes = await FingerprintInputsAsync(source);
        indexDirectory ??= Path.Combine(Path.GetTempPath(), "Rei-engine-tests", "benchmark-index");
        ExternalProjectCopy.ValidateDestination(source, indexDirectory);
        Directory.CreateDirectory(indexDirectory);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToUpperInvariant())));
        var index = Path.Combine(indexDirectory, key + ".json");
        if (File.Exists(index))
        {
            if ((File.GetAttributes(index) & FileAttributes.ReparsePoint) != 0) throw new IOException("Benchmark index must not be a link.");
            var entry = JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(index))!;
            if (string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase) &&
                entry.Hashes.Count == hashes.Count && hashes.All(hash => entry.Hashes.TryGetValue(hash.Key, out var value) && value == hash.Value) &&
                Directory.Exists(Path.Combine(entry.RunDirectory, "project")) && File.Exists(Path.Combine(entry.RunDirectory, "storage", "preferences.json")))
                return new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source,
                    startupTimeout: startupTimeout, preparedRunDirectory: entry.RunDirectory);
        }
        var engine = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source, startupTimeout: startupTimeout);
        Directory.CreateDirectory(engine.RunDirectory);
        await File.WriteAllTextAsync(index, JsonSerializer.Serialize(new Entry(source, engine.RunDirectory, hashes)));
        return engine;
    }

}
