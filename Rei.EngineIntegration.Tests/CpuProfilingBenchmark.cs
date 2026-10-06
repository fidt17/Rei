using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rei.EngineIntegration.Tests;

internal static class CpuProfilingBenchmark
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new() { WriteIndented = true };

    internal static void RequireCurrentEngineBuild()
    {
        var engineFile = Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE");
        if (string.IsNullOrWhiteSpace(engineFile) || !File.Exists(engineFile))
            throw new InvalidOperationException("Set REI_TEST_ENGINE_FILE to an existing file.");
        var settings = JsonNode.Parse(File.ReadAllText(engineFile))!;
        var root = Path.GetDirectoryName(Path.GetFullPath(engineFile))!;
        var binary = Path.Combine(root, settings["RelativeDebugIncludeDir"]!.GetValue<string>().TrimStart('\\', '/'), "Rei.dll");
        var inputs = EngineInputs(root).Where(File.Exists).ToArray();
        ValidateBuildTimestamps(binary, inputs);
    }

    internal static void ValidateBuildTimestamps(string binary, IEnumerable<string> inputs)
    {
        if (!File.Exists(binary)) throw new FileNotFoundException("Build current Debug engine before CPU benchmark.", binary);
        foreach (var input in inputs)
            if (File.GetLastWriteTimeUtc(binary) < File.GetLastWriteTimeUtc(input))
                throw new InvalidDataException($"Engine binary predates source; rebuild Rei Debug before launching benchmark: {input}");
    }

    private static IEnumerable<string> EngineInputs(string root) => new[]
    {
        "Rei/src/Core.h", "Rei/resources/rei_data_assets/render/RendererSettings.h",
        "Rei/resources/rei_behaviours/render/MeshRenderer.h", "Rei/resources/rei_behaviours/render/MeshRenderer.cpp",
        "Rei/resources/rei_behaviours/render/SpriteRenderer.h", "Rei/resources/rei_behaviours/render/SpriteRenderer.cpp",
        "Rei/src/Modules/Physics/Collider.h", "Rei/src/Modules/Physics/ModelCollider.h", "Rei/src/Modules/Physics/ModelCollider.cpp",
        "Rei/src/Common/Math/Bounds.h", "Rei/src/Modules/Render/Model/Model.h", "Rei/src/Modules/Render/Model/Model.cpp",
        "Rei/src/Common/Profiling/GpuTimer.h", "Rei/src/Modules/Render/RenderScenario/DefaultRenderScenario.h",
        "Rei/src/Common/Profiling/ProfileMarkers.h", "Rei/src/Common/Profiling/ProfilingService.h", "Rei/src/Common/Profiling/ProfilingService.cpp",
        "Rei/src/Modules/Render/Shaders/Shader.h", "Rei/src/Modules/Render/Shaders/Shader.cpp",
        "Rei/src/Modules/Render/Modules/LightingRenderModule.h", "Rei/src/Modules/Render/Modules/LightingRenderModule.cpp",
        "Rei/src/Modules/Render/Material/Material.h", "Rei/src/Modules/Render/Material/Material.cpp",
        "Rei/src/Modules/Render/Renderer.cpp", "Rei/src/Modules/Render/RenderScenario/DefaultRenderScenario.cpp"
    }.Select(path => Path.Combine(root, path));

    internal static async Task CaptureAsync(EngineIntegrationHarness engine, int warmupFrames = 120, int framesPerCapture = 120, int captureCount = 3)
    {
        if (warmupFrames is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(warmupFrames));
        if (framesPerCapture is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(framesPerCapture));
        if (captureCount is < 2 or > 10) throw new ArgumentOutOfRangeException(nameof(captureCount));
        RequireCurrentEngineBuild();
        // StartAsync already waits for the Editor's incremental build and asset import.
        // Capture must not force Clean/Rebuild or restart an already current runtime.
        var readyUtc = DateTimeOffset.UtcNow;
        var buildLogs = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "info", ["limit"] = 500 });
        await WriteAsync(engine, "cpu-build-logs.json", buildLogs);
        var before = await engine.CallAsync("rei_editor_get_state");
        var projectFile = Directory.GetFiles(engine.ProjectDirectory, "*.rei").Single();
        var project = JsonNode.Parse(await File.ReadAllTextAsync(projectFile))!;
        var projectName = project["ProjectName"]!.GetValue<string>();
        var sceneId = project["LastSceneId"]?.GetValue<string>();
        var manifest = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var directory in new[] { "Project", "Internal" })
        {
            var path = Path.Combine(engine.ProjectDirectory, directory);
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                manifest.Add(Path.GetRelativePath(engine.ProjectDirectory, file), await DescribeFileAsync(file));
        }
        var engineFile = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_TEST_ENGINE_FILE")!);
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(engineFile))!;
        var nativeDirectory = Path.Combine(Path.GetDirectoryName(engineFile)!,
            settings["RelativeDebugIncludeDir"]!.GetValue<string>().TrimStart('\\', '/'));
        var binaries = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["editor"] = await DescribeFileAsync(Environment.GetEnvironmentVariable("REI_TEST_EDITOR_EXE")!),
            ["engineSettings"] = await DescribeFileAsync(engineFile),
            ["engine"] = await DescribeFileAsync(Path.Combine(nativeDirectory, "Rei.dll"))
        };
        var engineInputs = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var file in EngineInputs(Path.GetDirectoryName(engineFile)!)) engineInputs.Add(file, await DescribeFileAsync(file));
        var projectDll = Path.Combine(engine.ProjectDirectory, "bin", "x64EditorDebug", projectName, projectName + ".dll");
        Assert.True(File.Exists(projectDll), $"Prepared build did not produce project DLL: {projectDll}");
        binaries["projectDll"] = await DescribeFileAsync(projectDll);
        var copiedEngine = Path.Combine(Path.GetDirectoryName(projectDll)!, "Rei.dll");
        Assert.True(File.Exists(copiedEngine), "Prepared build did not copy current engine beside project DLL.");
        await using (var original = File.OpenRead(Path.Combine(nativeDirectory, "Rei.dll")))
        await using (var copy = File.OpenRead(copiedEngine))
            Assert.Equal(await SHA256.HashDataAsync(original), await SHA256.HashDataAsync(copy));
        binaries["ownedEngineCopy"] = await DescribeFileAsync(copiedEngine);
        await WriteAsync(engine, "cpu-benchmark-context.json", new
        {
            schemaVersion = 1, capturedUtc = DateTimeOffset.UtcNow, engine.ProcessId, engine.ProjectDirectory,
            startupTimeoutSeconds = engine.StartupTimeout.TotalSeconds,
            configuration = "EditorDebug", platform = "x64", projectName, sceneId, readyUtc,
            warmupFrames, framesPerCapture, captureCount, binaries, engineInputs, manifest,
            editorStateBefore = before,
            camera = new { status = "unavailable", source = "runtime", reason = "Existing MCP does not expose active native camera or Editor fly-camera matrices." },
            viewport = new { status = "unavailable", source = "runtime", reason = "Existing non-image MCP reads do not expose native framebuffer dimensions." },
            layout = new { source = "harness", preferences = await File.ReadAllTextAsync(Path.Combine(engine.RunDirectory, "storage", "preferences.json")),
                note = "Fresh isolated storage; old user layout is not copied. Old viewport/camera baseline equivalence is unverified." },
            measurement = "Native CPU wall time including GL waits; optional asynchronous GPU timestamp counters (nanoseconds / samples). No framebuffer readback inside captures.",
            gpuTimingRequested = Environment.GetEnvironmentVariable("REI_PROFILE_GPU") == "1",
            swapIntervalRequested = Environment.GetEnvironmentVariable("REI_PROFILE_SWAP_INTERVAL"),
            presentationNote = "Actual swap interval is logged by native Renderer; request alone does not prove a driver cap is disabled."
        });
        var warmup = await CaptureOneAsync(engine, warmupFrames, null);
        await WriteAsync(engine, "cpu-warmup.json", warmup);
        var session = warmup.GetProperty("sessionId").GetString()!;
        var results = new List<object>();
        for (var capture = 1; capture <= captureCount; capture++)
        {
            var profile = await CaptureOneAsync(engine, framesPerCapture, session);
            var file = $"cpu-profile-{capture:D2}.json";
            await WriteAsync(engine, file, profile);
            ValidateCapture(profile, framesPerCapture, "EditorMode");
            Assert.Equal(0UL, Metric(profile, "Rei.Render.Readback").GetProperty("calls").GetUInt64());
            Assert.Equal(0UL, Metric(profile, "Rei.Picking.Selection").GetProperty("calls").GetUInt64());
            Assert.Equal(0UL, Metric(profile, "Rei.Picking.SelectionCandidates").GetProperty("value").GetUInt64());
            results.Add(new
            {
                file, sessionId = session, captureId = profile.GetProperty("captureId").GetString(),
                frameMs = profile.GetProperty("averageFrameMs").GetDouble(),
                exclusiveFrameMs = profile.GetProperty("metrics").EnumerateArray().Sum(metric => metric.GetProperty("averageExclusiveMs").GetDouble()),
                workload = profile.GetProperty("metrics").EnumerateArray().Where(metric => metric.GetProperty("kind").GetString() == "counter").ToArray()
            });
        }
        var after = await engine.CallAsync("rei_editor_get_state");
        Assert.Equal(before.GetProperty("scene").GetRawText(), after.GetProperty("scene").GetRawText());
        Assert.False(after.GetProperty("automation").GetProperty("isBuilding").GetBoolean());
        Assert.False(after.GetProperty("automation").GetProperty("isImporting").GetBoolean());
        await WriteAsync(engine, "cpu-benchmark-results.json", new { captures = results, editorStateAfter = after });
    }

    private static async Task<JsonElement> CaptureOneAsync(EngineIntegrationHarness engine, int frames, string? session)
    {
        var started = await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = frames });
        Assert.Equal("queued", started.GetProperty("status").GetString());
        var captureId = started.GetProperty("captureId").GetString();
        var captureSession = started.GetProperty("sessionId").GetString();
        if (session != null) Assert.Equal(session, captureSession);
        JsonElement profile = default;
        // Sparse reads reduce inspection work while the timed frames run. No image calls or input replay.
        await engine.WaitUntilAsync(async () =>
        {
            await Task.Delay(1000);
            profile = await engine.CallAsync("rei_editor_get_profiling_snapshot", new()
            {
                ["source"] = "runtime", ["view"] = "last_capture", ["expectedSessionId"] = captureSession, ["limit"] = 256
            });
            Assert.Contains(profile.GetProperty("status").GetString(), new[] { "ok", "no_samples" });
            Assert.Equal(captureId, profile.GetProperty("captureId").GetString());
            return profile.GetProperty("captureState").GetString() == "complete";
        }, TimeSpan.FromMinutes(4));
        ValidateCapture(profile, frames, "EditorMode");
        return profile;
    }

    internal static void ValidateCapture(JsonElement profile, int frames, string mode)
    {
        Assert.Equal("runtime", profile.GetProperty("source").GetString());
        Assert.Equal("ok", profile.GetProperty("status").GetString());
        Assert.Equal(mode, profile.GetProperty("engineMode").GetString());
        Assert.Equal("complete", profile.GetProperty("captureState").GetString());
        Assert.Equal(frames, profile.GetProperty("completedFrames").GetInt32());
        Assert.Equal(frames, profile.GetProperty("sampleFrames").GetInt32());
        Assert.True(profile.GetProperty("completeData").GetBoolean());
        Assert.False(profile.GetProperty("truncated").GetBoolean());
        Assert.Equal(0, profile.GetProperty("invalidFrames").GetInt32());
        Assert.Equal(0, profile.GetProperty("droppedScopes").GetInt32());
        var metrics = profile.GetProperty("metrics").EnumerateArray().ToArray();
        Assert.Equal(profile.GetProperty("metricCount").GetInt32(), metrics.Length);
        Assert.Equal(metrics.Length, metrics.Select(metric => metric.GetProperty("name").GetString()).Distinct().Count());
        foreach (var metric in metrics)
            Assert.InRange(metric.GetProperty("exclusiveMs").GetDouble(), 0, metric.GetProperty("inclusiveMs").GetDouble() + 0.001);
        Assert.InRange(metrics.Sum(metric => metric.GetProperty("exclusiveMs").GetDouble()), 0, profile.GetProperty("durationMs").GetDouble() + 0.001);
        ulong phaseUploads = 0;
        foreach (var phase in new[] { "Lighting", "Camera", "Object", "Material", "Other" })
            phaseUploads += Metric(profile, $"Rei.Shader.UniformUploads.{phase}").GetProperty("value").GetUInt64();
        var total = Metric(profile, "Rei.Shader.UniformUploads").GetProperty("value").GetUInt64();
        Assert.Equal(total, phaseUploads);
        // A uniform batch binds once for several uploads; call counts are independent.
        if (total > 0) Assert.True(Metric(profile, "Rei.Shader.UseCalls").GetProperty("value").GetUInt64() > 0);
    }

    private static JsonElement Metric(JsonElement profile, string name) =>
        Assert.Single(profile.GetProperty("metrics").EnumerateArray(), metric => metric.GetProperty("name").GetString() == name);

    private static async Task<object> DescribeFileAsync(string file)
    {
        if (!File.Exists(file)) return new { path = file, status = "unavailable" };
        await using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        var info = new FileInfo(file);
        return new { path = file, status = "available", sha256 = hash, bytes = info.Length, modifiedUtc = info.LastWriteTimeUtc };
    }

    private static Task WriteAsync(EngineIntegrationHarness engine, string name, object value) =>
        File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, name), JsonSerializer.Serialize(value, JSON_OPTIONS));
}
