using System.Text.Json;
using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

public sealed class ExternalBenchmarkFactAttribute : FactAttribute
{
    public ExternalBenchmarkFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "CPU benchmark requires Windows and a graphics session.";
        else if (Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")))
            Skip = "Set REI_RUN_ENGINE_TESTS=1 and REI_PROFILE_PROJECT to opt into external-project CPU benchmark.";
    }
}

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Benchmark")]
[Trait("Area", "Profiling")]
public sealed class CpuProfilingBenchmarkTests(ITestOutputHelper output)
{
    [ExternalBenchmarkFact]
    public async Task IsolatedExternalProjectProducesRepeatedNativeCpuCaptures()
    {
        var source = Path.GetFullPath(Environment.GetEnvironmentVariable("REI_PROFILE_PROJECT")!);
        CpuProfilingBenchmark.RequireCurrentEngineBuild();
        var timeoutSeconds = Environment.GetEnvironmentVariable("REI_PROFILE_STARTUP_TIMEOUT_SECONDS");
        TimeSpan? timeout = timeoutSeconds == null ? null : TimeSpan.FromSeconds(int.Parse(timeoutSeconds));
        await using var engine = new EngineIntegrationHarness(sourceProjectDirectory: source, startupTimeout: timeout);
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        var before = await ExternalProjectCopy.FingerprintAsync(source);
        Directory.CreateDirectory(engine.RunDirectory);
        await File.WriteAllTextAsync(Path.Combine(engine.RunDirectory, "cpu-source-manifest.json"), JsonSerializer.Serialize(new { source, hashes = before }));
        try
        {
            await engine.StartAsync();
            await CpuProfilingBenchmark.CaptureAsync(engine);
        }
        finally
        {
            await engine.DisposeAsync();
            var after = await ExternalProjectCopy.FingerprintAsync(source);
            Assert.Equal(before.ToArray(), after.ToArray());
        }
    }
}
