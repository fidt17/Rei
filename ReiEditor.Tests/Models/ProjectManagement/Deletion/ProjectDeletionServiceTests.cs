using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Deletion;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement.Deletion;

/// <summary>Verifies recursive deletion remains within the selected temporary project and logs failures.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Projects")]
public sealed class ProjectDeletionServiceTests
{
    /// <summary>Deleting a project removes nested files while preserving a sibling project.</summary>
    [Fact]
    public void DeletesOnlySelectedProjectTree()
    {
        using var directory = new TemporaryDirectory();
        var project = new Project();
        project.SetProjectFilePath(directory.GetPath("Selected", "Game.rei"));
        var nested = directory.GetPath("Selected", "Project", "Scenes");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Main.scene"), "scene");
        File.WriteAllText(project.ProjectFilePath, "project");
        var sibling = directory.GetPath("Sibling", "Keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        File.WriteAllText(sibling, "keep");
        var logger = new TestLogger<ProjectDeletionService>();

        new ProjectDeletionService(logger).DeleteProject(project);

        Assert.False(Directory.Exists(project.GetDirectoryPath()));
        Assert.Equal("keep", File.ReadAllText(sibling));
        Assert.DoesNotContain(logger.Entries, entry => entry.Exception != null);
    }

    /// <summary>A missing project directory is logged without throwing or deleting another directory.</summary>
    [Fact]
    public void MissingProjectLogsFailure()
    {
        using var directory = new TemporaryDirectory();
        var project = new Project();
        project.SetProjectFilePath(directory.GetPath("Missing", "Game.rei"));
        var logger = new TestLogger<ProjectDeletionService>();

        new ProjectDeletionService(logger).DeleteProject(project);

        Assert.IsType<DirectoryNotFoundException>(Assert.Single(logger.Entries, entry => entry.Exception != null).Exception);
        Assert.True(Directory.Exists(directory.RootPath));
    }
}
