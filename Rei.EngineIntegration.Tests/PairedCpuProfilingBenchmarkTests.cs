using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

public sealed class PairedBenchmarkFactAttribute : FactAttribute
{
    public PairedBenchmarkFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_REBUILD_GATE")))
            Skip = "Paired benchmark requires Windows, engine opt-in, project, and an explicit rebuild gate.";
    }
}

public sealed class ResumeBenchmarkFactAttribute : FactAttribute
{
    public ResumeBenchmarkFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_RESUME_RUN")))
            Skip = "Resume benchmark requires Windows, engine opt-in, project, and an existing owned run.";
    }
}

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Benchmark")]
[Trait("Area", "Profiling")]
public sealed class PairedCpuProfilingBenchmarkTests(ITestOutputHelper output)
{
    [PairedBenchmarkFact]
    public async Task SameOwnedProjectProducesBeforeAndAfterNativeCaptures()
    {
        var source = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")!);
        var gate = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_REBUILD_GATE")!);
        if (File.Exists(gate) || File.Exists(gate + ".ready")) throw new InvalidOperationException("Use a fresh rebuild gate.");
        CpuProfilingBenchmark.RequireCurrentEngineBuild();
        await using var engine = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source);
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var before = await ExternalProjectCopy.FingerprintAsync(source);
        try
        {
            await engine.StartAsync();
            await CapturePhaseAsync(engine, "before");
            // The caller rebuilds Rei and ReiSandbox sequentially, then creates the gate.
            // Keep the same owned project, imported caches and Editor storage for both phases.
            await engine.CloseEditorAsync();
            await File.WriteAllTextAsync(gate + ".ready", JsonSerializer.Serialize(new { engine.RunDirectory, engine.ProjectDirectory }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            while (!File.Exists(gate)) await Task.Delay(1000, timeout.Token);
            CpuProfilingBenchmark.RequireCurrentEngineBuild();
            await engine.RestartAsync();
            await CapturePhaseAsync(engine, "after");
            ValidateWorkload(engine.RunDirectory);
        }
        finally
        {
            await engine.DisposeAsync();
            var after = await ExternalProjectCopy.FingerprintAsync(source);
            Assert.Equal(before.ToArray(), after.ToArray());
        }
    }

    [ResumeBenchmarkFact]
    public async Task CompletedBaselineCanResumeAfterMeasurementOnSameOwnedProject()
    {
        var source = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")!);
        var run = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_RESUME_RUN")!);
        using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "before", "cpu-benchmark-context.json")));
        var processId = baseline.RootElement.GetProperty("ProcessId").GetInt32();
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            Assert.True(process.HasExited, "Stop the baseline's owned Editor before resuming.");
        }
        catch (ArgumentException) { }
        CpuProfilingBenchmark.RequireCurrentEngineBuild();
        await using var engine = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source, preparedRunDirectory: run);
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var before = await ExternalProjectCopy.FingerprintAsync(source);
        try
        {
            await engine.StartAsync();
            await CapturePhaseAsync(engine, "after");
            ValidateWorkload(engine.RunDirectory);
        }
        finally
        {
            await engine.DisposeAsync();
            Assert.Equal(before.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
        }
    }

    private static async Task CapturePhaseAsync(EngineIntegrationHarness engine, string phase)
    {
        var imagesBefore = Directory.GetFiles(engine.RunDirectory, "frame-*.png").ToHashSet(StringComparer.OrdinalIgnoreCase);
        await CpuProfilingBenchmark.CaptureAsync(engine);
        // Readback happens after all timed captures, never inside their measurement windows.
        var frame = await engine.CallAsync("rei_editor_capture_frame");
        var directory = Path.Combine(engine.RunDirectory, phase);
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(engine.RunDirectory, "cpu-*.json"))
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
        foreach (var file in Directory.GetFiles(engine.RunDirectory, "frame-*.png").Where(file => !imagesBefore.Contains(file)))
            File.Copy(file, Path.Combine(directory, "frame.png"), overwrite: false);
        await File.WriteAllTextAsync(Path.Combine(directory, "frame.json"), frame.GetRawText());
    }

    internal static void ValidateWorkload(string directory)
    {
        using var beforeFrame = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "before", "frame.json")));
        using var afterFrame = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "after", "frame.json")));
        foreach (var field in new[] { "width", "height", "engineMode" })
            Assert.Equal(beforeFrame.RootElement.GetProperty(field).GetRawText(), afterFrame.RootElement.GetProperty(field).GetRawText());
        for (var capture = 1; capture <= 3; ++capture)
        {
            var file = $"cpu-profile-{capture:D2}.json";
            using var before = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "before", file)));
            using var after = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "after", file)));
            foreach (var name in new[] { "Rei.Draw.Calls", "Rei.Draw.SubmittedVertices", "Rei.Draw.Triangles", "Rei.Material.Bindings" })
            {
                var first = Assert.Single(before.RootElement.GetProperty("metrics").EnumerateArray(), metric => metric.GetProperty("name").GetString() == name);
                var second = Assert.Single(after.RootElement.GetProperty("metrics").EnumerateArray(), metric => metric.GetProperty("name").GetString() == name);
                Assert.Equal(first.GetProperty("value").GetUInt64(), second.GetProperty("value").GetUInt64());
            }
        }
    }
}
