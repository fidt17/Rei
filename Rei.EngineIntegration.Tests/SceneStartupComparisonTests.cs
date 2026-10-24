using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

public sealed class SceneStartupComparisonFactAttribute : FactAttribute
{
    public SceneStartupComparisonFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_STARTUP_VARIANTS")))
            Skip = "Scene startup comparison requires Windows, engine opt-in, project, and an ordered engine variant manifest.";
    }
}

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Benchmark")]
[Trait("Area", "SceneLoading")]
public sealed class SceneStartupComparisonTests(ITestOutputHelper output)
{
    [SceneStartupComparisonFact]
    public async Task FreshEditorsLoadSameSceneWithSameWarmPackAndOrderedEngineVariants()
    {
        var source = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")!);
        var variants = JsonNode.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("REI_PROFILE_STARTUP_VARIANTS")!))!.AsArray();
        Assert.True(variants.Count >= 4, "Use a balanced order such as A, B, B, A.");
        var originalEngineFile = Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!;
        await using var prepared = await PreparedBenchmarkProject.OpenAsync(source);
        await prepared.PrepareProjectAsync(originalEngineFile, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        output.WriteLine($"Artifacts: {prepared.RunDirectory}");
        var sourceBefore = await ExternalProjectCopy.FingerprintAsync(source);
        var projectFile = Directory.GetFiles(prepared.ProjectDirectory, "*.rei").Single();
        var projectBytes = await File.ReadAllBytesAsync(projectFile);
        var preferencesFile = Path.Combine(prepared.RunDirectory, "storage", "preferences.json");
        var preferencesBytes = await File.ReadAllBytesAsync(preferencesFile);
        var scenes = Directory.EnumerateFiles(Path.Combine(prepared.ProjectDirectory, "Project"), "*.scene", SearchOption.AllDirectories)
            .Select(path => new
            {
                Id = JsonNode.Parse(File.ReadAllText(path + ".meta"))!["AssetId"]!.GetValue<string>(),
                Data = JsonNode.Parse(File.ReadAllText(path))!
            }).ToArray();
        var initialId = JsonNode.Parse(projectBytes)!["LastSceneId"]!.GetValue<string>();
        var control = scenes.Where(scene => scene.Id != initialId).MinBy(scene => scene.Data["Entities"]!.AsArray().Count)!;
        var assets = SceneLoadingBenchmarkTests.ReadDependencies(prepared.ProjectDirectory, scenes.Single(scene => scene.Id == initialId).Data);
        var controlAssets = SceneLoadingBenchmarkTests.ReadDependencies(prepared.ProjectDirectory, control.Data);
        var pack = Path.Combine(prepared.ProjectDirectory, "bin", "Resources", "assets.bin");
        var resultsFile = Path.Combine(prepared.RunDirectory, $"startup-comparison-{Guid.NewGuid():N}.jsonl");
        string? expectedPackHash = null, expectedFrameHash = null;
        SortedDictionary<string, string>? expectedResidency = null;
        var processes = new HashSet<int>();
        try
        {
            foreach (var variant in variants)
            {
                var name = variant!["name"]!.GetValue<string>();
                var engineFile = Path.GetFullPath(variant["engineFile"]!.GetValue<string>());
                var settings = JsonNode.Parse(await File.ReadAllTextAsync(engineFile))!;
                var binary = Path.GetFullPath(Path.GetDirectoryName(engineFile) + settings["RelativeDebugIncludeDir"]!.GetValue<string>() + "\\Rei.dll");
                var binaryHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(binary)));
                await File.WriteAllBytesAsync(projectFile, projectBytes);
                var preferences = JsonNode.Parse(preferencesBytes)!;
                preferences["EnginePath"] = engineFile;
                await File.WriteAllTextAsync(preferencesFile, preferences.ToJsonString());
                Environment.SetEnvironmentVariable("REI_TEST_ENGINE_FILE", engineFile);
                // Warm exactly the same packed input immediately before each fresh Editor process.
                // No source hashing or build runs compete with the timed native scene load.
                var warmup = Stopwatch.StartNew();
                var packHash = await PreparedBenchmarkProject.WarmPackedAssetsAsync(prepared.ProjectDirectory);
                var warmupMs = warmup.Elapsed.TotalMilliseconds;
                expectedPackHash ??= packHash;
                Assert.Equal(expectedPackHash, packHash);
                await using var engine = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source, preparedRunDirectory: prepared.RunDirectory);
                await engine.StartAsync(recordLoadTimings: true);
                Assert.True(processes.Add(engine.ProcessId), "Each variant must start a fresh Editor process.");
                using var process = Process.GetProcessById(engine.ProcessId);
                var module = Assert.Single(process.Modules.Cast<ProcessModule>(), module => module.ModuleName.Equals("Rei.dll", StringComparison.OrdinalIgnoreCase));
                Assert.StartsWith(prepared.ProjectDirectory, module.FileName, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(binaryHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(module.FileName))));
                var state = await engine.CallAsync("rei_editor_get_state");
                Assert.Equal(initialId, state.GetProperty("scene").GetProperty("id").GetString());
                var residency = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var asset in assets)
                {
                    var native = await engine.ReadAssetAsync(asset.Key, "runtime");
                    Assert.Equal("runtime", native.GetProperty("source").GetString());
                    residency.Add(asset.Key, native.GetProperty("status").GetString()!);
                }
                expectedResidency ??= residency;
                Assert.Equal(expectedResidency.ToArray(), residency.ToArray());
                var framesBefore = Directory.GetFiles(engine.RunDirectory, "frame-*.png").ToHashSet(StringComparer.OrdinalIgnoreCase);
                await engine.CallAsync("rei_editor_capture_frame");
                var image = Assert.Single(Directory.GetFiles(engine.RunDirectory, "frame-*.png"), path => !framesBefore.Contains(path));
                var frameHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(image)));
                expectedFrameHash ??= frameHash;
                Assert.Equal(expectedFrameHash, frameHash);
                await engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = control.Id });
                foreach (var asset in assets.Where(asset => SceneLoadingBenchmarkTests.IsSceneRenderAsset(asset.Value) && !controlAssets.ContainsKey(asset.Key)))
                    Assert.Equal("unloaded", (await engine.ReadAssetAsync(asset.Key, "runtime")).GetProperty("status").GetString());
                await engine.CloseEditorAsync();
                var timingFile = Path.Combine(engine.RunDirectory, $"load-timings-{engine.LaunchCount}.jsonl");
                var measurements = File.ReadLines(timingFile).Select(line => JsonNode.Parse(line)!["data"]!).ToArray();
                var load = Assert.Single(measurements, row => row["stage"]!.GetValue<string>() == "scene.load" && row["sceneId"]!.GetValue<string>() == initialId);
                Assert.True(load["success"]!.GetValue<bool>());
                // Exclude control-scene reads from the startup totals.
                var initialLoadEnd = Array.IndexOf(measurements, load);
                var textures = measurements.Take(initialLoadEnd).Where(row => row["stage"]!.GetValue<string>() == "texture.read").ToArray();
                await File.AppendAllTextAsync(resultsFile, JsonSerializer.Serialize(new
                {
                    name, engineFile, binaryHash, packHash, packBytes = new FileInfo(pack).Length, warmupMs,
                    engine.ProcessId, engine.LaunchCount, timingFile, image, frameHash, assetCount = residency.Count,
                    load, preload = measurements.Take(initialLoadEnd).Single(row => row["stage"]!.GetValue<string>() == "scene.preload"),
                    textureCount = textures.Length, textureBytes = textures.Sum(row => row["bytes"]!.GetValue<long>()),
                    textureTotalMs = textures.Sum(row => row["totalMs"]!.GetValue<double>()),
                    textureCopyMs = textures.Sum(row => row["copyMs"]?.GetValue<double>() ?? 0)
                }) + Environment.NewLine);
            }
            await using var finalPack = File.OpenRead(pack);
            Assert.Equal(expectedPackHash, Convert.ToHexString(await SHA256.HashDataAsync(finalPack)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("REI_TEST_ENGINE_FILE", originalEngineFile);
            await File.WriteAllBytesAsync(projectFile, projectBytes);
            await File.WriteAllBytesAsync(preferencesFile, preferencesBytes);
            Assert.Equal(sourceBefore.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
        }
    }
}
