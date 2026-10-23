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
    public async Task IsolatedExternalProjectRecordsSceneUnloadReloadAndNativeAssetResidency()
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
            await engine.StartAsync();
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
                Assert.True(longestUiResponse < TimeSpan.FromSeconds(2), $"Editor UI blocked for {longestUiResponse}. Artifacts: {engine.RunDirectory}");
                Assert.True(loadingResponses > 0, $"Editor UI did not answer while loading. Artifacts: {engine.RunDirectory}");
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
                    residency.Add(asset.Key, new { name = asset.Value, status = state.GetProperty("status").GetString() });
                }
                using var process = Process.GetProcessById(processId);
                phases.Add(new
                {
                    phase, utc = DateTimeOffset.UtcNow, transitionMs, editor, native, residency,
                    workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
                    totalCpuMs = process.TotalProcessorTime.TotalMilliseconds
                });
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
