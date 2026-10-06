namespace Rei.EngineIntegration.Tests;

public sealed class CpuBenchmarkPrerequisiteTests
{
    [Fact]
    public async Task ExternalStartupBudgetIsConfigurableAndFixtureDefaultIsPreserved()
    {
        await using var fixture = new EngineIntegrationHarness();
        await using var external = new EngineIntegrationHarness(sourceProjectDirectory: Path.GetTempPath());
        await using var configured = new EngineIntegrationHarness(sourceProjectDirectory: Path.GetTempPath(), startupTimeout: TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(4), fixture.StartupTimeout);
        Assert.Equal(TimeSpan.FromMinutes(15), external.StartupTimeout);
        Assert.Equal(TimeSpan.FromMinutes(10), configured.StartupTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineIntegrationHarness(startupTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineIntegrationHarness(startupTimeout: TimeSpan.FromMinutes(31)));
        Assert.Equal(0, external.LaunchCount);
    }

    [Fact]
    public async Task StaleEngineBinaryIsRejectedBeforeLaunchingOwnedEditor()
    {
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: true);
        Directory.CreateDirectory(harness.RunDirectory);
        try
        {
            var binary = Path.Combine(harness.RunDirectory, "Rei.dll");
            var source = Path.Combine(harness.RunDirectory, "Collider.h");
            await File.WriteAllTextAsync(binary, "previous build");
            await File.WriteAllTextAsync(source, "changed virtual interface");
            var timestamp = DateTime.UtcNow.AddMinutes(-1);
            File.SetLastWriteTimeUtc(binary, timestamp);
            File.SetLastWriteTimeUtc(source, timestamp.AddSeconds(1));
            Assert.Throws<InvalidDataException>(() => CpuProfilingBenchmark.ValidateBuildTimestamps(binary, new[] { source }));
            File.SetLastWriteTimeUtc(binary, timestamp.AddSeconds(2));
            CpuProfilingBenchmark.ValidateBuildTimestamps(binary, new[] { source });
            Assert.Equal(0, harness.LaunchCount);
            Assert.Throws<FileNotFoundException>(() => CpuProfilingBenchmark.ValidateBuildTimestamps(Path.Combine(harness.RunDirectory, "missing.dll"), new[] { source }));
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }
}
