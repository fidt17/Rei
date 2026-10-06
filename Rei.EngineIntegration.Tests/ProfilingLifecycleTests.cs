using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
[Trait("Area", "Profiling")]
public sealed class ProfilingLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task NativeCaptureCountsProjectDllScopesAndResetsAcrossPlayStopAndReload()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        await engine.CallAsync("rei_editor_save_project");
        var files = Directory.GetFiles(Path.Combine(engine.ProjectDirectory, "Project"), "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".asset") || path.EndsWith(".mat") || path.EndsWith(".scene"))
            .ToDictionary(path => path, File.ReadAllBytes);
        string? previousSession = null;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await engine.RunOperationAsync("rei_editor_start_playmode");
            var empty = await Read(engine, "recent");
            Assert.Equal("disabled", empty.GetProperty("status").GetString());
            Assert.Equal(0, empty.GetProperty("completedFrames").GetInt32());
            var session = empty.GetProperty("sessionId").GetString()!;
            Assert.NotEqual(previousSession, session);
            previousSession = session;
            var started = await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 40 });
            Assert.Equal("queued", started.GetProperty("status").GetString());
            var captureId = started.GetProperty("captureId").GetString();
            await engine.WaitUntilAsync(async () => (await Read(engine, "last_capture")).GetProperty("captureState").GetString() == "complete");
            var result = await Read(engine, "last_capture");
            Assert.Equal("runtime", result.GetProperty("source").GetString());
            Assert.Equal("ok", result.GetProperty("status").GetString());
            Assert.Equal(session, result.GetProperty("sessionId").GetString());
            Assert.Equal(captureId, result.GetProperty("captureId").GetString());
            Assert.Equal(40, result.GetProperty("completedFrames").GetInt32());
            Assert.Equal(40, result.GetProperty("sampleFrames").GetInt32());
            Assert.True(result.GetProperty("completeData").GetBoolean());
            var metrics = result.GetProperty("metrics").EnumerateArray().ToArray();
            Assert.False(result.GetProperty("truncated").GetBoolean());
            Assert.Equal(0, result.GetProperty("invalidFrames").GetInt32());
            Assert.Equal(0, result.GetProperty("droppedScopes").GetInt32());
            foreach (var name in new[] { "Rei.Render.Outline.Pass", "Rei.Render.Geometry", "Rei.Render.Lighting.Apply",
                "Rei.Render.ObjectData", "Rei.Render.Mesh.Submit", "Rei.Render.Helpers", "Rei.Render.Output", "Rei.Render.Outline.Composite" })
                Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == name);
            var uploads = Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == "Rei.Shader.UniformUploads")
                .GetProperty("value").GetUInt64();
            ulong phaseUploads = 0;
            foreach (var phase in new[] { "Lighting", "Camera", "Object", "Material", "Other" })
                phaseUploads += Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == $"Rei.Shader.UniformUploads.{phase}")
                    .GetProperty("value").GetUInt64();
            Assert.Equal(uploads, phaseUploads);
            var useCalls = Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == "Rei.Shader.UseCalls")
                .GetProperty("value").GetUInt64();
            Assert.True(useCalls >= uploads);
            var exclusiveMs = metrics.Sum(metric => metric.GetProperty("exclusiveMs").GetDouble());
            Assert.InRange(exclusiveMs, 0, result.GetProperty("durationMs").GetDouble() + 0.001);
            var scope = Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == "Fixture.Profiling.Update");
            Assert.Equal(40, scope.GetProperty("calls").GetInt32());
            Assert.True(scope.GetProperty("inclusiveMs").GetDouble() >= scope.GetProperty("exclusiveMs").GetDouble());
            var counter = Assert.Single(metrics, metric => metric.GetProperty("name").GetString() == "Fixture.Profiling.Count");
            Assert.Equal(120, counter.GetProperty("value").GetInt32());
            Assert.Equal(result.GetRawText(), (await Read(engine, "last_capture")).GetRawText());
            var mismatch = await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["expectedSessionId"] = "0" });
            Assert.Equal("session_changed", mismatch.GetProperty("status").GetString());
            var limited = await engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["view"] = "last_capture", ["limit"] = 2 });
            Assert.Equal(2, limited.GetProperty("metrics").GetArrayLength());
            Assert.True(limited.GetProperty("truncated").GetBoolean());
            var longCapture = await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 3600 });
            var busy = await engine.CallAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 1 });
            Assert.Equal("busy", busy.GetProperty("status").GetString());
            Assert.Equal(longCapture.GetProperty("captureId").GetString(), busy.GetProperty("captureId").GetString());
            var reads = Enumerable.Range(0, 12).Select(_ => engine.CallAsync("rei_editor_get_profiling_snapshot",
                new() { ["view"] = "last_capture", ["expectedSessionId"] = session })).ToArray();
            await engine.RunOperationAsync("rei_editor_stop_playmode");
            foreach (var read in await Task.WhenAll(reads))
            {
                var status = read.GetProperty("status").GetString();
                Assert.Contains(status, new[] { "ok", "no_samples", "session_changed", "engine_unavailable" });
                if (status != "no_samples") continue;
                Assert.Equal(session, read.GetProperty("sessionId").GetString());
                Assert.Equal(longCapture.GetProperty("captureId").GetString(), read.GetProperty("captureId").GetString());
                Assert.Equal(0, read.GetProperty("completedFrames").GetInt32());
            }
            JsonElement afterStop = default;
            await engine.WaitUntilAsync(async () =>
            {
                afterStop = await Read(engine, "last_capture");
                return afterStop.GetProperty("status").GetString() != "engine_unavailable";
            });
            Assert.Equal("disabled", afterStop.GetProperty("status").GetString());
            Assert.NotEqual(session, afterStop.GetProperty("sessionId").GetString());
            Assert.Equal(0, afterStop.GetProperty("completedFrames").GetInt32());
            foreach (var (path, bytes) in files) Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            if (cycle == 1)
                await engine.RunOperationAsync("rei_editor_start_build", new() { ["configuration"] = "editor_debug", ["forceSolutionRebuild"] = true });
        }
        await engine.AssertToolErrorAsync("rei_editor_start_profiling_capture", new() { ["frameCount"] = 0 }, "invalid_frame_count");
        await engine.AssertToolErrorAsync("rei_editor_get_profiling_snapshot", new() { ["source"] = "editor" }, "invalid_source");
        var errors = await engine.CallAsync("rei_editor_get_logs", new() { ["minimumLevel"] = "error", ["limit"] = 500 });
        Assert.Empty(errors.GetProperty("entries").EnumerateArray());
    }

    private static Task<JsonElement> Read(EngineIntegrationHarness engine, string view) =>
        engine.CallAsync("rei_editor_get_profiling_snapshot", new() { ["source"] = "runtime", ["view"] = view });
}
