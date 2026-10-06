using System.Security.Cryptography;
using Xunit.Abstractions;
using static Rei.EngineIntegration.Tests.AssetAssertions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "Build")]
public sealed class BuildIncrementalityLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task RestartAndSingleSourceEditPreserveUnchangedObjectsAndFailedBuildKeepsLiveDll()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await WaitForConfigAsync(engine);
        var scripts = Path.Combine(engine.ProjectDirectory, "Project", "Scripts");
        var project = Path.Combine(scripts, "EngineFixture.vcxproj");
        var registry = Path.Combine(scripts, "Internal", "BehaviourRegistry.cpp");
        var intermediates = Path.Combine(engine.ProjectDirectory, "bin", "int", "x64EditorDebug");
        var pch = Path.Combine(intermediates, "ReiProject.pch");
        var objects = Directory.GetFiles(intermediates, "*.obj", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);
        Assert.True(objects.Count >= 3, "Expected main, registry and PCH objects.");
        Assert.True(File.Exists(pch));
        var stableFiles = new[] { project, registry, pch }.ToDictionary(path => path, File.GetLastWriteTimeUtc);

        var cache = Path.Combine(engine.ProjectDirectory, "bin", "Resources", "Cache");
        using var initialManifest = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(cache, "asset-cache.json")));
        var referenceName = initialManifest.RootElement.GetProperty("Entries").GetProperty("rei_cube.obj").GetProperty("CacheFileName").GetString()!;
        var serialModelBytes = await File.ReadAllBytesAsync(Path.Combine(cache, referenceName));
        var cube = Path.Combine(engine.ProjectDirectory, "Project", "Engine Resources", "meshes", "cube.obj");
        for (var i = 0; i < 2; i++)
        {
            var folder = Path.Combine(engine.ProjectDirectory, "Project", "ParallelModels", i.ToString());
            Directory.CreateDirectory(folder);
            var model = Path.Combine(folder, "cube.obj");
            await File.WriteAllTextAsync(model + ".meta", System.Text.Json.JsonSerializer.Serialize(new { AssetId = "parallel-model-" + i, Data = new { } }));
            File.Copy(cube, model);
        }
        await engine.RestartAsync();
        await WaitForConfigAsync(engine);
        foreach (var (path, timestamp) in objects.Concat(stableFiles)) Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));

        using var parallelManifest = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(cache, "asset-cache.json")));
        for (var i = 0; i < 2; i++)
        {
            var name = parallelManifest.RootElement.GetProperty("Entries").GetProperty("parallel-model-" + i).GetProperty("CacheFileName").GetString()!;
            Assert.Equal(serialModelBytes, await File.ReadAllBytesAsync(Path.Combine(cache, name)));
        }
        await SetAsync(engine, "_floatValue", 31.5);
        await engine.CallAsync("rei_editor_save_project");
        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug" });
        foreach (var (path, timestamp) in objects.Concat(stableFiles)) Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        var archive = Path.Combine(engine.ProjectDirectory, "bin", "Resources", "assets.bin");
        var archiveTimestamp = File.GetLastWriteTimeUtc(archive);
        var archiveHash = SHA256.HashData(await File.ReadAllBytesAsync(archive));
        var source = Path.Combine(scripts, "ReiApp.cpp");
        var original = await File.ReadAllBytesAsync(source);
        await File.AppendAllTextAsync(source, "\r\n// Incremental build regression.\r\n");
        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug" });
        var mainObject = Assert.Single(objects.Keys, path => Path.GetFileName(path) == "ReiApp.obj");
        Assert.NotEqual(objects[mainObject], File.GetLastWriteTimeUtc(mainObject));
        Assert.Equal(archiveTimestamp, File.GetLastWriteTimeUtc(archive));
        Assert.Equal(archiveHash, SHA256.HashData(await File.ReadAllBytesAsync(archive)));
        foreach (var (path, timestamp) in objects.Where(pair => pair.Key != mainObject).Concat(stableFiles))
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await AssertDiskFloatAsync(engine, 31.5);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForConfigAsync(engine);

        var liveDll = Path.Combine(engine.ProjectDirectory, "bin", "x64EditorDebug", "EngineFixture", "EngineFixture.dll");
        var liveHash = SHA256.HashData(await File.ReadAllBytesAsync(liveDll));
        await File.AppendAllTextAsync(source, "\r\n#error REI_INCREMENTAL_BUILD_TEST\r\n");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunOperationAsync(
            "rei_editor_start_build", new() { ["configuration"] = "editor_debug" }));
        Assert.Contains("build_failed", failure.Message);
        Assert.Equal(liveHash, SHA256.HashData(await File.ReadAllBytesAsync(liveDll)));
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await File.WriteAllBytesAsync(source, original);
        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug" });
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 31.5);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForConfigAsync(engine);
        await engine.CallAsync("rei_editor_capture_frame");
        await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "debug" });
        var standalone = await engine.RunStandaloneSmokeAsync(@"bin\x64Debug\EngineFixture\EngineFixture.exe");
        Assert.Contains("DataAsset test observed float=31.5", standalone);
    }
}
