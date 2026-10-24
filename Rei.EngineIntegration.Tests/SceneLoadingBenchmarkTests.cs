using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Benchmark")]
[Trait("Area", "SceneLoading")]
public sealed class SceneLoadingBenchmarkTests(ITestOutputHelper output)
{
    [ExternalBenchmarkFact]
    public Task IsolatedExternalProjectRecordsSceneUnloadReloadAndNativeAssetResidency() => RecordSceneCycleAsync(recordLoadTimings: false);

    [ExternalBenchmarkFact]
    public Task IsolatedExternalProjectRecordsDetailedSceneLoadTimings() => RecordSceneCycleAsync(recordLoadTimings: true);

    private async Task RecordSceneCycleAsync(bool recordLoadTimings)
    {
        var source = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")!);
        CpuProfilingBenchmark.RequireCurrentEngineBuild();
        await using var engine = await PreparedBenchmarkProject.OpenAsync(source);
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var before = await ExternalProjectCopy.FingerprintAsync(source);
        using var cancellation = new CancellationTokenSource();
        var sampling = engine.SampleProcessResourcesAsync(cancellation.Token);
        try
        {
            await engine.StartAsync(recordLoadTimings);
            var initial = (await engine.CallAsync("rei_editor_get_state")).GetProperty("scene").Clone();
            var initialId = initial.GetProperty("id").GetString()!;
            var scenes = Directory.EnumerateFiles(Path.Combine(engine.ProjectDirectory, "Project"), "*.scene", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Path = path,
                    Id = JsonNode.Parse(File.ReadAllText(path + ".meta"))!["AssetId"]!.GetValue<string>(),
                    Data = JsonNode.Parse(File.ReadAllText(path))!
                }).ToArray();
            var control = scenes.Where(scene => scene.Id != initialId).MinBy(scene => scene.Data["Entities"]!.AsArray().Count)
                ?? throw new InvalidDataException("Scene cycle benchmark requires a second scene.");
            var assets = ReadDependencies(engine.ProjectDirectory, scenes.Single(scene => scene.Id == initialId).Data);
            var controlAssets = ReadDependencies(engine.ProjectDirectory, control.Data);
            var phases = new List<object>();
            var session = "";
            double initialTriangles = 0;
            var processId = engine.ProcessId;
            await CaptureAsync("initial", initialId, 0);
            foreach (var (phase, id) in new[] { ("unload", control.Id), ("reload", initialId), ("unload-again", control.Id) })
            {
                var started = DateTimeOffset.UtcNow;
                var elapsed = Stopwatch.StartNew();
                var transition = engine.RunOperationAsync("rei_editor_open_scene", new() { ["scene"] = id });
                var longestUiResponse = TimeSpan.Zero;
                var loadingResponses = 0;
                while (!transition.IsCompleted)
                {
                    await Task.Delay(250);
                    var responseTime = Stopwatch.StartNew();
                    var state = await engine.CallAsync("rei_editor_get_state");
                    responseTime.Stop();
                    if (responseTime.Elapsed > longestUiResponse) longestUiResponse = responseTime.Elapsed;
                    if (state.GetProperty("scene").ValueKind == JsonValueKind.Null) loadingResponses++;
                    await engine.AppendDiagnosticAsync("scene-ui-responsiveness.jsonl", JsonSerializer.Serialize(new
                    {
                        phase, utc = DateTimeOffset.UtcNow, responseMs = responseTime.Elapsed.TotalMilliseconds,
                        status = state.GetProperty("status").GetString()
                    }));
                }
                await transition;
                // The timing benchmark records UI response without making it a performance gate.
                if (!recordLoadTimings)
                {
                    Assert.True(longestUiResponse < TimeSpan.FromSeconds(2), $"Editor UI blocked for {longestUiResponse}. Artifacts: {engine.RunDirectory}");
                    // Fast transitions can finish between UI polls; only sustained work needs a loading response.
                    if (elapsed.Elapsed >= TimeSpan.FromSeconds(2))
                        Assert.True(loadingResponses > 0, $"Editor UI did not answer while loading. Artifacts: {engine.RunDirectory}");
                }
                await engine.AppendDiagnosticAsync("scene-transitions.jsonl", JsonSerializer.Serialize(new
                {
                    phase, sceneId = id, startedUtc = started, completedUtc = DateTimeOffset.UtcNow,
                    milliseconds = elapsed.Elapsed.TotalMilliseconds
                }));
                await CaptureAsync(phase, id, elapsed.Elapsed.TotalMilliseconds);
            }
            var close = Stopwatch.StartNew();
            await engine.CloseEditorAsync();
            await engine.RecordTimingAsync("close-editor-after-scene-cycle", close.Elapsed);
            if (recordLoadTimings)
            {
                var timings = File.ReadLines(Path.Combine(engine.RunDirectory, $"load-timings-{engine.LaunchCount}.jsonl"))
                    .Select(line =>
                    {
                        using var document = JsonDocument.Parse(line);
                        return document.RootElement.Clone();
                    }).ToArray();
                Assert.NotEmpty(timings);
                Assert.True(timings.Zip(timings.Skip(1)).All(pair =>
                    pair.First.GetProperty("timestampNs").GetUInt64() <= pair.Second.GetProperty("timestampNs").GetUInt64()));
                var measurements = timings.Select(entry => entry.GetProperty("data")).ToArray();
                foreach (var measurement in measurements)
                    foreach (var property in measurement.EnumerateObject().Where(property => property.Name.EndsWith("Ms", StringComparison.Ordinal)))
                        Assert.True(double.IsFinite(property.Value.GetDouble()) && property.Value.GetDouble() >= 0, measurement.ToString());
                var loads = measurements.Where(entry => entry.GetProperty("stage").GetString() == "scene.load").ToArray();
                Assert.Equal(4, loads.Length); // Startup, unload, reload, second unload.
                Assert.All(loads, load => Assert.True(load.GetProperty("success").GetBoolean()));
                foreach (var id in new[] { initialId, control.Id })
                    Assert.Equal(2, loads.Count(load => load.GetProperty("sceneId").GetString() == id));
                var expectedModels = assets.Values.Count(name => Path.GetExtension(name).Equals(".obj", StringComparison.OrdinalIgnoreCase));
                foreach (var stage in new[] { "mesh.read", "model.read", "mesh.gpu", "model.release" })
                    Assert.True(measurements.Count(entry => entry.GetProperty("stage").GetString() == stage) >= expectedModels * 2, stage);
                Assert.Contains(measurements, entry => entry.GetProperty("stage").GetString() == "registry.scan");
                var loadingScene = false;
                foreach (var measurement in measurements)
                {
                    var stage = measurement.GetProperty("stage").GetString();
                    if (stage == "scene.load.begin") loadingScene = true;
                    if (loadingScene) Assert.NotEqual("registry.scan", stage);
                    if (stage == "scene.load") loadingScene = false;
                }
                Assert.False(loadingScene);
                foreach (var stage in new[] { "texture.read", "texture.gpu", "texture.release" })
                    Assert.Contains(measurements, entry => entry.GetProperty("stage").GetString() == stage);
                var summary = measurements.GroupBy(entry => entry.GetProperty("stage").GetString()!)
                    .ToDictionary(group => group.Key, group => new
                    {
                        count = group.Count(),
                        milliseconds = group.SelectMany(entry => entry.EnumerateObject())
                            .Where(property => property.Name.EndsWith("Ms", StringComparison.Ordinal))
                            .GroupBy(property => property.Name)
                            .ToDictionary(values => values.Key, values => values.Sum(value => value.Value.GetDouble()))
                    });
                await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, $"load-timing-summary-{engine.LaunchCount}.json"),
                    JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            }

            async Task CaptureAsync(string phase, string sceneId, double transitionMs)
            {
                var capture = await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 2 });
                JsonElement native = default;
                await engine.WaitUntilAsync(async () =>
                {
                    native = await engine.CallAsync("rei_editor_get_profiling_snapshot", new()
                    {
                        ["source"] = "runtime", ["view"] = "last_capture", ["limit"] = 256,
                        ["expectedSessionId"] = capture.GetProperty("sessionId").GetString()
                    });
                    return native.GetProperty("captureState").GetString() == "complete";
                }, TimeSpan.FromMinutes(2));
                CpuProfilingBenchmark.ValidateCapture(native, 2, "EditorMode");
                var triangles = native.GetProperty("metrics").EnumerateArray()
                    .Single(metric => metric.GetProperty("name").GetString() == "Rei.Draw.Triangles")
                    .GetProperty("averageValue").GetDouble();
                if (phase == "initial")
                {
                    session = native.GetProperty("sessionId").GetString()!;
                    initialTriangles = triangles;
                }
                Assert.Equal(session, native.GetProperty("sessionId").GetString());
                Assert.Equal(processId, engine.ProcessId);
                if (sceneId == initialId) Assert.Equal(initialTriangles, triangles);
                var editor = await engine.CallAsync("rei_editor_get_state");
                Assert.Equal(sceneId, editor.GetProperty("scene").GetProperty("id").GetString());
                var residency = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var asset in assets)
                {
                    var state = await engine.ReadAssetAsync(asset.Key, "runtime");
                    Assert.Equal("runtime", state.GetProperty("source").GetString());
                    Assert.Contains(state.GetProperty("status").GetString(), new[] { "loaded", "unloaded", "unsupported" });
                    if (sceneId == control.Id && IsSceneRenderAsset(asset.Value) && !controlAssets.ContainsKey(asset.Key))
                        Assert.True(state.GetProperty("status").GetString() == "unloaded", $"Scene render asset retained after unload: {asset.Key} ({asset.Value}).");
                    residency.Add(asset.Key, new { name = asset.Value, status = state.GetProperty("status").GetString() });
                }
                using var process = Process.GetProcessById(processId);
                phases.Add(new
                {
                    phase, utc = DateTimeOffset.UtcNow, transitionMs, editor, native, residency,
                    workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
                    totalCpuMs = process.TotalProcessorTime.TotalMilliseconds
                });
                if (recordLoadTimings)
                {
                    // Read pixels only after timed captures and resource snapshots.
                    var existing = Directory.GetFiles(engine.RunDirectory, "frame-*.png").ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var frame = await engine.CallAsync("rei_editor_capture_frame");
                    var image = Assert.Single(Directory.GetFiles(engine.RunDirectory, "frame-*.png"), path => !existing.Contains(path));
                    File.Copy(image, Path.Combine(engine.RunDirectory, $"scene-{phase}.png"), overwrite: true);
                    await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, $"scene-{phase}.json"), frame.GetRawText());
                }
                await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, "scene-cycle-results.json"), JsonSerializer.Serialize(new
                {
                    source, initialScene = initial, controlScene = new { control.Id, control.Path }, phases
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            cancellation.Cancel();
            await sampling;
            await engine.DisposeAsync();
            Assert.Equal(before.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
        }
    }

    private static bool IsSceneRenderAsset(string name) => Path.GetExtension(name).ToLowerInvariant() is ".obj" or ".mat" or ".jpg" or ".jpeg" or ".png" or ".tga" or ".bmp";

    private static SortedDictionary<string, string> ReadDependencies(string project, JsonNode scene)
    {
        var resources = Path.Combine(project, "bin", "Resources");
        var map = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "map.json")))!["Assets"]!.AsArray()
            .ToDictionary(asset => asset!["Id"]!.GetValue<string>(), asset => asset!);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        Collect(scene);
        using var pack = new BinaryReader(File.OpenRead(Path.Combine(resources, "assets.bin")));
        while (pending.TryDequeue(out var id))
        {
            var asset = map[id];
            if (!asset["Name"]!.GetValue<string>().EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            pack.BaseStream.Position = asset["Offset"]!.GetValue<long>();
            Collect(JsonNode.Parse(pack.ReadBytes(pack.ReadInt32()))!);
        }
        return new SortedDictionary<string, string>(ids.ToDictionary(id => id, id => map[id]["Name"]!.GetValue<string>()), StringComparer.Ordinal);

        void Collect(JsonNode? node)
        {
            if (node is JsonObject obj) foreach (var item in obj) Collect(item.Value);
            else if (node is JsonArray array) foreach (var item in array) Collect(item);
            else if (node is JsonValue value && value.TryGetValue<string>(out var id) && map.ContainsKey(id) && ids.Add(id)) pending.Enqueue(id);
        }
    }
}
