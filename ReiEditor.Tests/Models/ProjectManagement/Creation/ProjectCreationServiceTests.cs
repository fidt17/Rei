using Newtonsoft.Json.Linq;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement.Creation;

/// <summary>Verifies project creation persistence, notification timing and cleanup inside owned temporary directories.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Projects")]
public sealed class ProjectCreationServiceTests
{
    private sealed class TestSerializer(Exception failure) : ISerializer
    {
        public string Serialize<T>(T value) => throw failure;
        public T Deserialize<T>(string source) => throw new NotSupportedException();
        public T Deserialize<T>(string source, T defaultValue) => throw new NotSupportedException();
    }

    /// <summary>Creation writes project metadata with generated solution paths before publishing success.</summary>
    [Fact]
    public async Task CreatesProjectFileBeforePublishingSuccess()
    {
        using var directory = new TemporaryDirectory();
        var before = DateTime.UtcNow;
        var generated = new SolutionGenerationResult
        {
            SolutionPath = directory.GetPath("Game", "Game.sln"),
            ProjectPath = directory.GetPath("Game", "Project", "Game.vcxproj")
        };
        ProjectCreationConfiguration? observed = null;
        var generator = new TestSolutionGenerator
        {
            OnGenerate = configuration =>
            {
                observed = configuration;
                Assert.True(Directory.Exists(configuration.FullPath));
                return Task.FromResult(generated);
            }
        };
        // The current implementation ignores the storage provider; all writable paths are replaced before execution.
        var service = new ProjectCreationService(null!, new TestLogger<ProjectCreationService>(), new JsonSerializer(), generator);
        service.Configuration.ParentDirectoryPath = directory.RootPath;
        service.Configuration.ProjectName = "Game";
        var published = new List<Project>();
        var failures = 0;
        service.ProjectCreationFailedEvent += () => failures++;
        service.ProjectCreatedEvent += project =>
        {
            Assert.True(File.Exists(project.ProjectFilePath));
            published.Add(project);
        };

        var result = await service.CreateProject();

        Assert.NotNull(result);
        Assert.Same(result, Assert.Single(published));
        Assert.Same(service.Configuration, observed);
        Assert.Equal(0, failures);
        Assert.Equal("Game", result.ProjectName);
        Assert.Equal(directory.GetPath("Game", "Game.rei"), result.ProjectFilePath);
        Assert.Equal(generated.SolutionPath, result.ProjectSolutionPath);
        Assert.Equal(generated.ProjectPath, result.ProjectVisualStudioProjectPath);
        Assert.InRange(result.LastEditTime, before, DateTime.UtcNow);
        Assert.False(result.HasBeenSetup);
        var json = JObject.Parse(await File.ReadAllTextAsync(result.ProjectFilePath));
        Assert.Equal("Game", json.Value<string>(nameof(Project.ProjectName)));
        Assert.Equal(generated.ProjectPath, json.Value<string>(nameof(Project.ProjectVisualStudioProjectPath)));
        Assert.Equal(generated.SolutionPath, json.Value<string>(nameof(Project.ProjectSolutionPath)));
        Assert.Null(json.Property(nameof(Project.ProjectFilePath)));
    }

    /// <summary>A generation failure removes partial project files before publishing failure and preserves sibling data.</summary>
    [Fact]
    public async Task GenerationFailureCleansPartialOutputBeforeNotifying()
    {
        using var directory = new TemporaryDirectory();
        var keep = directory.GetPath("Keep.txt");
        await File.WriteAllTextAsync(keep, "keep");
        var failure = new IOException("controlled generation failure");
        var generator = new TestSolutionGenerator
        {
            OnGenerate = async configuration =>
            {
                await File.WriteAllTextAsync(Path.Combine(configuration.FullPath, "partial.sln"), "partial");
                throw failure;
            }
        };
        var logger = new TestLogger<ProjectCreationService>();
        var service = new ProjectCreationService(null!, logger, new JsonSerializer(), generator);
        service.Configuration.ParentDirectoryPath = directory.RootPath;
        service.Configuration.ProjectName = "FailedProject";
        var successes = 0;
        var failures = 0;
        service.ProjectCreatedEvent += _ => successes++;
        service.ProjectCreationFailedEvent += () =>
        {
            Assert.False(Directory.Exists(service.Configuration.FullPath));
            failures++;
        };

        Assert.Null(await service.CreateProject());

        Assert.Equal(0, successes);
        Assert.Equal(1, failures);
        Assert.Contains(logger.Entries, entry => ReferenceEquals(entry.Exception, failure));
        Assert.Equal("keep", await File.ReadAllTextAsync(keep));
        Assert.False(Directory.Exists(service.Configuration.FullPath));
    }

    /// <summary>Serialization failure after successful generation removes generated output and publishes only failure.</summary>
    [Fact]
    public async Task SerializationFailureCleansGeneratedOutput()
    {
        using var directory = new TemporaryDirectory();
        var failure = new InvalidOperationException("controlled serialization failure");
        var generated = false;
        var generator = new TestSolutionGenerator
        {
            OnGenerate = async configuration =>
            {
                var solution = Path.Combine(configuration.FullPath, "Game.sln");
                var project = Path.Combine(configuration.FullPath, "Game.vcxproj");
                await File.WriteAllTextAsync(solution, "solution");
                await File.WriteAllTextAsync(project, "project");
                generated = true;
                return new SolutionGenerationResult { SolutionPath = solution, ProjectPath = project };
            }
        };
        var logger = new TestLogger<ProjectCreationService>();
        var service = new ProjectCreationService(null!, logger, new TestSerializer(failure), generator);
        service.Configuration.ParentDirectoryPath = directory.RootPath;
        service.Configuration.ProjectName = "Game";
        var successes = 0;
        var failures = 0;
        service.ProjectCreatedEvent += _ => successes++;
        service.ProjectCreationFailedEvent += () => failures++;

        Assert.Null(await service.CreateProject());

        Assert.True(generated);
        Assert.Equal(0, successes);
        Assert.Equal(1, failures);
        Assert.False(Directory.Exists(service.Configuration.FullPath));
        Assert.Contains(logger.Entries, entry => ReferenceEquals(entry.Exception, failure));
    }
}
