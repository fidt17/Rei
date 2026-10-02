namespace Rei.EngineIntegration.Tests;

public sealed class HarnessCleanupTests
{
    [Fact]
    public async Task DisposeTrimsBuildOutputsButPreservesSourcesAndDiagnostics()
    {
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: false);
        try
        {
            var binary = Path.Combine(harness.ProjectDirectory, "bin", "int", "fixture.obj");
            Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
            await File.WriteAllTextAsync(binary, "build output");
            var source = Path.Combine(harness.ProjectDirectory, "fixture.cpp");
            await File.WriteAllTextAsync(source, "source");
            await harness.AppendDiagnosticAsync("mcp.jsonl", "evidence");
            await harness.DisposeAsync();
            Assert.False(Directory.Exists(Path.Combine(harness.ProjectDirectory, "bin")));
            Assert.Equal("source", await File.ReadAllTextAsync(source));
            Assert.Contains("evidence", await File.ReadAllTextAsync(Path.Combine(harness.RunDirectory, "mcp.jsonl")));
            await harness.DisposeAsync();
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }

    [Fact]
    public async Task ExplicitOptOutPreservesBuildOutputs()
    {
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: true);
        try
        {
            var binary = Path.Combine(harness.ProjectDirectory, "bin", "fixture.pdb");
            Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
            await File.WriteAllTextAsync(binary, "symbols");
            await harness.DisposeAsync();
            Assert.Equal("symbols", await File.ReadAllTextAsync(binary));
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("..")]
    public void RejectsPathsOutsideImmediateGuidRuns(string name)
    {
        Assert.Throws<IOException>(() => EngineIntegrationHarness.TrimBuildOutputs(
            Path.Combine(Path.GetTempPath(), "Rei-engine-tests", name)));
    }

    [Fact]
    public void RejectsGuidDirectoryOutsideHarnessRoot()
    {
        Assert.Throws<IOException>(() => EngineIntegrationHarness.TrimBuildOutputs(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task LockedOutputReportsCleanupFailureAndAllowsRetry()
    {
        if (!OperatingSystem.IsWindows()) return; // Unix permits unlinking an open file.
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: false);
        try
        {
            var binary = Path.Combine(harness.ProjectDirectory, "bin", "fixture.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
            await File.WriteAllTextAsync(binary, "locked");
            using (var held = new FileStream(binary, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await harness.DisposeAsync();
                Assert.True(File.Exists(binary));
                Assert.True(File.Exists(Path.Combine(harness.RunDirectory, "cleanup-error.txt")));
            }
            await harness.DisposeAsync();
            Assert.False(Directory.Exists(Path.Combine(harness.ProjectDirectory, "bin")));
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }

    [Fact]
    public async Task LinkedBuildTreeIsRejectedWithoutTouchingTarget()
    {
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: true);
        var target = Path.Combine(harness.RunDirectory, "retained-target");
        var outputs = Path.Combine(harness.ProjectDirectory, "bin");
        try
        {
            Directory.CreateDirectory(target);
            await File.WriteAllTextAsync(Path.Combine(target, "important.txt"), "keep");
            Directory.CreateDirectory(outputs);
            var link = Path.Combine(outputs, "linked");
            if (OperatingSystem.IsWindows())
            {
                var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.ArgumentList.Add("-NoProfile");
                start.ArgumentList.Add("-NonInteractive");
                start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("New-Item -ItemType Junction -Path $env:REI_TEST_LINK -Target $env:REI_TEST_LINK_TARGET -ErrorAction Stop | Out-Null");
                start.Environment["REI_TEST_LINK"] = link;
                start.Environment["REI_TEST_LINK_TARGET"] = target;
                using var process = System.Diagnostics.Process.Start(start)!;
                var stderr = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                await stdout;
                Assert.True(process.ExitCode == 0, await stderr);
            }
            else Directory.CreateSymbolicLink(link, target);
            Assert.Throws<IOException>(() => EngineIntegrationHarness.TrimBuildOutputs(harness.RunDirectory));
            Assert.True(File.Exists(Path.Combine(target, "important.txt")));
            Directory.Delete(link);
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }
}
