using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.ProjectManagement.Template;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Controls solution generation and update without launching external build tools.</summary>
internal sealed class TestSolutionGenerator : ISolutionGenerator
{
    public Func<ProjectCreationConfiguration, Task<SolutionGenerationResult>>? OnGenerate { get; init; }
    public Func<string, Task>? OnUpdate { get; init; }

    public Task<SolutionGenerationResult> GenerateSolution(ProjectCreationConfiguration configuration)
        => (OnGenerate ?? throw new NotSupportedException())(configuration);

    public Task UpdateProjectFile(string projectFilePath)
        => (OnUpdate ?? throw new NotSupportedException())(projectFilePath);

    public Task AddSourceFiles(string projectFilePath, IEnumerable<string> includes) => throw new NotSupportedException();
}
