using ReiEditor.Models.ProjectManagement;

namespace ReiEditor.Tests.Models.ProjectManagement;

/// <summary>
/// Verifies project path normalization and identity semantics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "ProjectManagement")]
public sealed class ProjectTests
{
    /// <summary>
    /// Path setters normalize relative segments to absolute paths and expose project directory.
    /// </summary>
    [Fact]
    public void PathSettersNormalizePathsAndExposeProjectDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReiEditorTests", Guid.NewGuid().ToString("N"));
        var project = new Project();

        project.SetProjectFilePath(Path.Combine(root, "nested", "..", "Game.rei"));
        project.SetProjectSolutionPath(Path.Combine(root, ".", "Game.sln"));
        project.SetProjectVisualStudioProjectPath(Path.Combine(root, "src", "..", "Game.vcxproj"));

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "Game.rei")), project.ProjectFilePath);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "Game.sln")), project.ProjectSolutionPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "Game.vcxproj")), project.ProjectVisualStudioProjectPath);
        Assert.Equal(Path.GetFullPath(root), project.GetDirectoryPath());
    }

    /// <summary>
    /// Projects with equivalent normalized file paths compare equal.
    /// </summary>
    [Fact]
    public void EqualsUsesNormalizedProjectFilePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReiEditorTests", Guid.NewGuid().ToString("N"));
        var first = new Project();
        var second = new Project();
        var different = new Project();
        first.SetProjectFilePath(Path.Combine(root, "folder", "..", "Game.rei"));
        second.SetProjectFilePath(Path.Combine(root, "Game.rei"));
        different.SetProjectFilePath(Path.Combine(root, "Other.rei"));

        Assert.True(first.Equals(second));
        Assert.False(first.Equals(different));
    }
}
