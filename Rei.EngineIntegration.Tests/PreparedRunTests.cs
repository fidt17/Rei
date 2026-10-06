using System.Text.Json;

namespace Rei.EngineIntegration.Tests;

public sealed class PreparedRunTests
{
    [Fact]
    public async Task ResumePreservesPreparedSourcesPreferencesCachesAndLogSequence()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        try
        {
            Directory.CreateDirectory(owner.ProjectDirectory);
            var solution = Path.Combine(owner.ProjectDirectory, "Fixture.sln");
            var project = Path.Combine(owner.ProjectDirectory, "Fixture.vcxproj");
            await File.WriteAllTextAsync(solution, "localized solution");
            await File.WriteAllTextAsync(project, "localized project");
            await File.WriteAllTextAsync(Path.Combine(owner.ProjectDirectory, "Fixture.rei"), JsonSerializer.Serialize(new
            {
                ProjectSolutionPath = solution, ProjectVisualStudioProjectPath = project
            }));
            var storage = Path.Combine(owner.RunDirectory, "storage");
            Directory.CreateDirectory(storage);
            await File.WriteAllTextAsync(Path.Combine(storage, "preferences.json"), "retained preferences");
            await File.WriteAllTextAsync(Path.Combine(owner.ProjectDirectory, "imported.cache"), "retained cache");
            await File.WriteAllTextAsync(Path.Combine(owner.RunDirectory, "stdout-3.log"), "prior diagnostics");
            var before = await ExternalProjectCopy.FingerprintAsync(owner.ProjectDirectory);
            await using var resumed = new EngineIntegrationHarness(keepBuildOutputs: true, preparedRunDirectory: owner.RunDirectory);
            await resumed.PrepareProjectAsync("unused engine", "unused MSBuild");
            Assert.Equal(owner.ProjectDirectory, resumed.ProjectDirectory);
            Assert.Equal(3, resumed.LaunchCount);
            Assert.Equal(before.ToArray(), (await ExternalProjectCopy.FingerprintAsync(owner.ProjectDirectory)).ToArray());
            Assert.Equal("retained preferences", await File.ReadAllTextAsync(Path.Combine(storage, "preferences.json")));
            Assert.Equal("prior diagnostics", await File.ReadAllTextAsync(Path.Combine(owner.RunDirectory, "stdout-3.log")));
        }
        finally { Directory.Delete(owner.RunDirectory, recursive: true); }
    }

    [Fact]
    public void ResumeRejectsSourceOrArbitraryDirectory()
    {
        Assert.Throws<IOException>(() => new EngineIntegrationHarness(preparedRunDirectory: Path.GetTempPath()));
        Assert.Throws<IOException>(() => new EngineIntegrationHarness(preparedRunDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task ResumeRejectsIncompleteRunAndEscapingBuildReferences()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        try
        {
            Directory.CreateDirectory(owner.ProjectDirectory);
            Assert.Throws<InvalidDataException>(() => new EngineIntegrationHarness(preparedRunDirectory: owner.RunDirectory));
            Directory.CreateDirectory(Path.Combine(owner.RunDirectory, "storage"));
            await File.WriteAllTextAsync(Path.Combine(owner.RunDirectory, "storage", "preferences.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(owner.ProjectDirectory, "Fixture.rei"), JsonSerializer.Serialize(new
            {
                ProjectSolutionPath = Path.Combine(owner.RunDirectory, "outside.sln"),
                ProjectVisualStudioProjectPath = Path.Combine(owner.ProjectDirectory, "Fixture.vcxproj")
            }));
            await File.WriteAllTextAsync(Path.Combine(owner.RunDirectory, "outside.sln"), "keep");
            await File.WriteAllTextAsync(Path.Combine(owner.ProjectDirectory, "Fixture.vcxproj"), "project");
            Assert.Throws<InvalidDataException>(() => new EngineIntegrationHarness(preparedRunDirectory: owner.RunDirectory));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(owner.RunDirectory, "outside.sln")));
        }
        finally { Directory.Delete(owner.RunDirectory, recursive: true); }
    }
}
