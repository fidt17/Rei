namespace ReiEditor.Tests.Infrastructure.Fixtures;

/// <summary>
/// Verifies that temporary filesystem fixtures isolate their paths and cleanup from other tests.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "TestInfrastructure")]
public sealed class TemporaryDirectoryTests
{
    /// <summary>
    /// Disposing one fixture removes its nested files while preserving files owned by another fixture.
    /// </summary>
    [Fact]
    public void DisposeDeletesOnlyItsOwnDirectory()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var nestedPath = first.GetPath("nested");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(first.GetPath("nested", "owned.txt"), "owned");
        var otherPath = second.GetPath("keep.txt");
        File.WriteAllText(otherPath, "keep");

        first.Dispose();

        Assert.False(Directory.Exists(first.RootPath));
        Assert.Equal("keep", File.ReadAllText(otherPath));
    }

    /// <summary>
    /// Relative traversal and a different fixture's absolute root are rejected before files can be accessed.
    /// </summary>
    [Fact]
    public void GetPathRejectsTraversalAndAnotherFixtureRoot()
    {
        using var fixture = new TemporaryDirectory();
        using var other = new TemporaryDirectory();

        Assert.Throws<ArgumentException>(() => fixture.GetPath("..", "outside.txt"));
        Assert.Throws<ArgumentException>(() => fixture.GetPath(other.RootPath));
    }
}
