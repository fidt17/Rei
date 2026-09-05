using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Infrastructure.Fixtures;

public sealed class TemporaryProjectFixture : IDisposable
{
    public TemporaryDirectory Directory { get; } = new();
    public Project Project { get; } = new();
    public TestLogger<ResourceService> ResourceLogger { get; } = new();
    public ResourceService Resources { get; }

    public TemporaryProjectFixture()
    {
        Project.SetProjectName("Test project");
        Project.SetProjectFilePath(Directory.GetPath("TestProject.reiproj"));
        var activeProject = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        activeProject.OpenProject(Project);
        Resources = new ResourceService(ResourceLogger, activeProject, new JsonSerializer());
    }

    public void Dispose() => Directory.Dispose();
}
