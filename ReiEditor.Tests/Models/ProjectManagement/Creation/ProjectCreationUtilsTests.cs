using ReiEditor.Models.ProjectManagement.Creation;

namespace ReiEditor.Tests.Models.ProjectManagement.Creation;

/// <summary>Verifies defaults returned for new project creation.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ProjectManagement")]
public sealed class ProjectCreationUtilsTests
{
    /// <summary>Default configuration supplies expected name and a full path coherent with its parent.</summary>
    [Fact]
    public void TestCreatesCoherentDefaultConfiguration()
    {
        var configuration = ProjectCreationUtils.GetDefaultProjectCreationConfiguration(null!);

        Assert.Equal("New Project", configuration.ProjectName);
        Assert.False(string.IsNullOrWhiteSpace(configuration.ParentDirectoryPath));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(configuration.ParentDirectoryPath, configuration.ProjectName)),
            configuration.FullPath);
    }
}
