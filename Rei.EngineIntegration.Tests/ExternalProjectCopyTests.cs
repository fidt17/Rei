using System.Text.Json.Nodes;

namespace Rei.EngineIntegration.Tests;

public sealed class ExternalProjectCopyTests
{
    [Fact]
    public async Task AbsoluteSourcePathsAreLocalizedAndFullCopyRemainsIndependent()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = Path.Combine(owner.RunDirectory, "source");
        CreateSource(source);
        await using var clone = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source);
        try
        {
            var original = await ExternalProjectCopy.FingerprintAsync(source);
            await clone.PrepareProjectAsync(Path.Combine(source, "engine.rei_engine"), "msbuild.exe");
            var project = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(clone.ProjectDirectory, "Fixture.rei")))!;
            Assert.Equal(Path.Combine(clone.ProjectDirectory, "Fixture.sln"), project["ProjectSolutionPath"]!.GetValue<string>());
            var build = await File.ReadAllTextAsync(Path.Combine(clone.ProjectDirectory, "Scripts", "Fixture.vcxproj"));
            Assert.Contains(clone.ProjectDirectory, build);
            Assert.DoesNotContain(source, build);
            var registry = await File.ReadAllTextAsync(Path.Combine(clone.ProjectDirectory, "Scripts", "BehaviourRegistry.cpp"));
            Assert.Contains(clone.ProjectDirectory, registry);
            Assert.DoesNotContain(source, registry);
            Assert.Equal("cached binary", await File.ReadAllTextAsync(Path.Combine(clone.ProjectDirectory, "bin", "cached.dll")));
            Assert.False(File.Exists(Path.Combine(clone.ProjectDirectory, "bin", "compile.read.1.tlog")));
            Assert.True(File.Exists(Path.Combine(source, "bin", "compile.read.1.tlog")));
            await File.WriteAllTextAsync(Path.Combine(clone.ProjectDirectory, "asset.meta"), "new metadata");
            Assert.Equal(original.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
            await clone.PrepareProjectAsync("ignored-on-restart", "ignored-on-restart");
            Assert.Equal("new metadata", await File.ReadAllTextAsync(Path.Combine(clone.ProjectDirectory, "asset.meta")));
        }
        finally
        {
            if (Directory.Exists(clone.RunDirectory)) Directory.Delete(clone.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.sln")]
    [InlineData("../source-sibling/outside.sln")]
    public async Task ProjectReferencesOutsideSourceAreRejected(string path)
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = Path.Combine(owner.RunDirectory, "source");
        CreateSource(source);
        await using var clone = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source);
        try
        {
            var project = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(source, "Fixture.rei")))!;
            project["ProjectSolutionPath"] = path;
            await File.WriteAllTextAsync(Path.Combine(source, "Fixture.rei"), project.ToJsonString());
            await Assert.ThrowsAsync<InvalidDataException>(() => clone.PrepareProjectAsync("engine", "msbuild"));
        }
        finally
        {
            if (Directory.Exists(clone.RunDirectory)) Directory.Delete(clone.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("../../../../outside")]
    [InlineData("$(UnverifiedOutputRoot)/bin")]
    public async Task EscapingOrUnverifiableBuildOutputIsRejected(string output)
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = Path.Combine(owner.RunDirectory, "source");
        CreateSource(source);
        await using var clone = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source);
        try
        {
            var file = Path.Combine(source, "Scripts", "Fixture.vcxproj");
            await File.WriteAllTextAsync(file, $"<Project><PropertyGroup><OutDir>{output}</OutDir><IntDir>$(SolutionDir)bin/int</IntDir></PropertyGroup></Project>");
            await Assert.ThrowsAsync<InvalidDataException>(() => clone.PrepareProjectAsync("engine", "msbuild"));
        }
        finally
        {
            if (Directory.Exists(clone.RunDirectory)) Directory.Delete(clone.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LinkedSourceTreeIsRejectedBeforeCreatingProjectCopy()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = Path.Combine(owner.RunDirectory, "source");
        CreateSource(source);
        var target = Path.Combine(owner.RunDirectory, "retained-target");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "important.txt"), "keep");
        var link = Path.Combine(source, "linked");
        await using var clone = new EngineIntegrationHarness(keepBuildOutputs: true, sourceProjectDirectory: source);
        try
        {
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
            await Assert.ThrowsAsync<IOException>(() => clone.PrepareProjectAsync("engine", "msbuild"));
            Assert.False(Directory.Exists(clone.ProjectDirectory));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(target, "important.txt")));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            if (Directory.Exists(clone.RunDirectory)) Directory.Delete(clone.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DefaultFixtureStillUsesOwnedPathsAndEnginePlaceholder()
    {
        await using var harness = new EngineIntegrationHarness(keepBuildOutputs: true);
        try
        {
            var engine = Path.Combine(harness.RunDirectory, "engine", "ReiEngine.rei_engine");
            await harness.PrepareProjectAsync(engine, "msbuild.exe");
            var project = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(harness.ProjectDirectory, "EngineFixture.rei")))!;
            var vcxproj = project["ProjectVisualStudioProjectPath"]!.GetValue<string>();
            Assert.StartsWith(harness.ProjectDirectory + Path.DirectorySeparatorChar, vcxproj);
            var build = await File.ReadAllTextAsync(vcxproj);
            Assert.DoesNotContain("__REI_ROOT__", build);
            Assert.Contains(Path.GetDirectoryName(engine)!, build);
        }
        finally { Directory.Delete(harness.RunDirectory, recursive: true); }
    }

    private static void CreateSource(string source)
    {
        Directory.CreateDirectory(Path.Combine(source, "Scripts"));
        Directory.CreateDirectory(Path.Combine(source, "bin"));
        File.WriteAllText(Path.Combine(source, "Fixture.rei"), new JsonObject
        {
            ["ProjectName"] = "Fixture", ["ProjectSolutionPath"] = Path.Combine(source, "Fixture.sln"),
            ["ProjectVisualStudioProjectPath"] = Path.Combine(source, "Scripts", "Fixture.vcxproj")
        }.ToJsonString());
        File.WriteAllText(Path.Combine(source, "Fixture.sln"), "solution");
        File.WriteAllText(Path.Combine(source, "Scripts", "Fixture.vcxproj"),
            $"<Project><PropertyGroup><OutDir>{source}/bin/$(Platform)$(Configuration)/$(ProjectName)/</OutDir><IntDir>$(SolutionDir)bin/int/</IntDir></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(source, "Scripts", "BehaviourRegistry.cpp"), $"#include \"{source}/Scripts/Behaviour.h\"");
        File.WriteAllText(Path.Combine(source, "asset.meta"), "original metadata");
        File.WriteAllText(Path.Combine(source, "bin", "cached.dll"), "cached binary");
        File.WriteAllText(Path.Combine(source, "bin", "compile.read.1.tlog"), Path.Combine(source, "bin", "cached.dll"));
    }
}
