using System.Security.Cryptography;
using System.Text.Json.Nodes;
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
    public async Task NewerStaleEngineDllIsReplacedBeforeRestartWithoutRebuildingAssets()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        var runtime = Path.Combine(engine.ProjectDirectory, "bin", "x64EditorDebug", "EngineFixture", "Rei.dll");
        var expected = SHA256.HashData(await File.ReadAllBytesAsync(runtime));
        var archive = Path.Combine(engine.ProjectDirectory, "bin", "Resources", "assets.bin");
        var archiveTimestamp = File.GetLastWriteTimeUtc(archive);
        await engine.CloseEditorAsync();

        await engine.RestartAsync(async _ =>
        {
            await File.WriteAllTextAsync(runtime, "stale DLL with a newer timestamp");
            File.SetLastWriteTimeUtc(runtime, DateTime.UtcNow.AddDays(1));
        });

        Assert.Equal(expected, SHA256.HashData(await File.ReadAllBytesAsync(runtime)));
        Assert.Equal(archiveTimestamp, File.GetLastWriteTimeUtc(archive));
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 12.5);
        var logs = await engine.CloseEditorAsync();
        Assert.Contains("Build Complete", logs);
        Assert.Contains("Shutdown complete", logs);
    }

    [EngineFact]
    public async Task PchHeaderEditRebuildsConsumersAndRestartWithoutChangesPreservesObjects()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var engineFile = Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!;
        var engineRoot = Path.GetDirectoryName(engineFile)!;
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(engineFile))!;
        var overlay = Path.Combine(engine.RunDirectory, "engine");
        var headers = Path.Combine(overlay, "headers");
        Directory.CreateDirectory(headers);
        string RelativeEnginePath(string path) => "\\" + Path.GetRelativePath(overlay, engineRoot + path);
        foreach (var key in new[] { "RelativeDebugIncludeDir", "RelativeReleaseIncludeDir", "RelativeResourcesDir" })
            settings[key] = RelativeEnginePath(settings[key]!.GetValue<string>());
        settings["RelativeSourceIncludes"] = "\\headers;" + string.Join(';', settings["RelativeSourceIncludes"]!.GetValue<string>()
            .Split(';', StringSplitOptions.RemoveEmptyEntries).Select(RelativeEnginePath));
        var overlaySettings = Path.Combine(overlay, "ReiEngine.rei_engine");
        await File.WriteAllTextAsync(overlaySettings, settings.ToJsonString());
        await engine.PrepareProjectAsync(overlaySettings, Environment.GetEnvironmentVariable("REI_TEST_MSBUILD")!);
        var scripts = Path.Combine(engine.ProjectDirectory, "Project", "Scripts");
        var engineHeader = Path.Combine(engineRoot, "Rei", "src", "Rei.h").Replace('\\', '/');
        // Shadow only this fixture's forced include; never edit the shared engine headers.
        await File.WriteAllTextAsync(Path.Combine(headers, "Rei.h"), $"#pragma once\n#include \"PchValue.inl\"\n#include \"{engineHeader}\"\n");
        var header = Path.Combine(headers, "PchValue.inl");
        await File.WriteAllTextAsync(header, "inline constexpr auto PCH_TEST_VALUE = 11;\n");
        await File.WriteAllTextAsync(Path.Combine(scripts, "PchConsumer.cpp"), "i32 OtherPchValue() { return PCH_TEST_VALUE; }\n");
        var config = Path.Combine(scripts, "Scripts", "DataAssets", "DataAssetTestConfig.h");
        await File.WriteAllTextAsync(config, (await File.ReadAllTextAsync(config)).Replace("    public:",
            "    public:\n        void SetPchTestValues(i32 first, i32 second) { _signedValue = first; _unsignedValue = static_cast<u32>(second); }\n"));
        var main = Path.Combine(scripts, "ReiApp.cpp");
        var source = await File.ReadAllTextAsync(main);
        source = source.Replace("class TestApplication", "#include \"Scripts/DataAssets/DataAssetTestConfig.h\"\ni32 OtherPchValue();\nclass TestApplication")
            .Replace("rei::GetProfiler().Register(DESCRIPTORS);", "rei::GetProfiler().Register(DESCRIPTORS); auto config = rei::GetAssetManager().GetById<symbols::testing::DataAssetTestConfig>(\"" + CONFIG_ID + "\"); config->SetPchTestValues(PCH_TEST_VALUE, OtherPchValue());");
        await File.WriteAllTextAsync(main, source);
        await engine.StartAsync();
        async Task AssertRuntimeValues(int expected)
        {
            await WaitForConfigAsync(engine);
            var runtime = await engine.ReadAssetAsync(CONFIG_ID, "runtime");
            var values = runtime.GetProperty("values");
            Assert.Equal(expected, values.GetProperty("_signedValue").GetInt32());
            Assert.Equal(expected, values.GetProperty("_unsignedValue").GetInt32());
        }
        await AssertRuntimeValues(11);
        var intermediates = Path.Combine(engine.ProjectDirectory, "bin", "int", "x64EditorDebug");
        var outputs = Directory.GetFiles(intermediates, "*.obj", SearchOption.AllDirectories)
            .Append(Path.Combine(intermediates, "ReiProject.pch"))
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);
        Assert.True(outputs.Count >= 5, "Expected PCH and four objects, including two value consumers.");

        await engine.RestartAsync(_ => File.WriteAllTextAsync(header, "inline constexpr auto PCH_TEST_VALUE = 22;\n"));
        await AssertRuntimeValues(22);
        foreach (var (path, timestamp) in outputs) Assert.NotEqual(timestamp, File.GetLastWriteTimeUtc(path));
        outputs = outputs.Keys.ToDictionary(path => path, File.GetLastWriteTimeUtc);

        await engine.RestartAsync();
        foreach (var (path, timestamp) in outputs) Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        await AssertRuntimeValues(22);
        await engine.RunOperationAsync("rei_editor_start_playmode");
        await WaitForConfigAsync(engine);
        await AssertPropertyAsync(engine, "_floatValue", 12.5);
        await engine.RunOperationAsync("rei_editor_stop_playmode");
        await WaitForConfigAsync(engine);
    }

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
