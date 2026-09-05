using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Utils.Path;

namespace ReiEditor.Tests.Utils.Paths;

/// <summary>Verifies normalized path boundaries and extension-preserving names.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Paths")]
public sealed class PathUtilityTests
{
    /// <summary>Directory membership respects complete path segments, normalization, and Windows path casing.</summary>
    [Theory]
    [InlineData("Assets", true)]
    [InlineData("assets/Textures/image.png", true)]
    [InlineData("Assets/Textures/../image.png", true)]
    [InlineData("AssetsOther/image.png", false)]
    [InlineData("Assets/../Other/image.png", false)]
    public void DirectoryMembershipChecksNormalizedSegments(string relativePath, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "ReiPathTests");
        Assert.Equal(expected, Path.Combine(root, relativePath).IsUnderDirectory(Path.Combine(root, "Assets")));
    }

    /// <summary>Path equality resolves relative segments and ignores casing without reading files.</summary>
    [Fact]
    public void PathEqualityNormalizesDotSegmentsAndCase()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReiPathTests");
        Assert.True(Path.Combine(root, "Assets", "..", "Assets", "Test.png").PathEquals(Path.Combine(root, "assets", "test.PNG")));
        Assert.False(Path.Combine(root, "a.png").PathEquals(Path.Combine(root, "b.png")));
    }

    /// <summary>File rename values omit the last extension; directory names retain their dots.</summary>
    [Theory]
    [InlineData("sky.diffuse.PNG", false, "sky.diffuse", "new.PNG")]
    [InlineData("folder.v1", true, "folder.v1", "new")]
    [InlineData("README", false, "README", "new")]
    public void RenamePreservesFileExtensionOnly(string original, bool isDirectory, string editValue, string renamed)
    {
        Assert.Equal(editValue, PathNamingUtils.GetRenameValue(original, isDirectory));
        Assert.Equal(renamed, PathNamingUtils.GetRenamedName(original, "new", isDirectory));
    }
}

/// <summary>Verifies generated filesystem paths against collisions in isolated directories.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Paths")]
public sealed class PathNamingFileSystemTests
{
    /// <summary>Unique file and directory paths avoid both file and directory collisions and start numbering at two.</summary>
    [Fact]
    public void UniquePathsAvoidBothKindsOfExistingEntry()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.GetPath("asset.mat"), "");
        Directory.CreateDirectory(directory.GetPath("asset 2.mat"));
        Directory.CreateDirectory(directory.GetPath("Folder"));
        File.WriteAllText(directory.GetPath("Folder 2"), "");

        Assert.Equal(directory.GetPath("asset 3.mat"), PathNamingUtils.GetUniqueFilePath(directory.RootPath, "asset.mat"));
        Assert.Equal(directory.GetPath("Folder 3"), PathNamingUtils.GetUniqueDirectoryPath(directory.RootPath, "Folder"));
        Assert.True(PathExtensions.PathExists(directory.GetPath("Folder"), isDirectory: true));
        Assert.False(PathExtensions.PathExists(directory.GetPath("Folder"), isDirectory: false));
    }

    /// <summary>Asset names check matching extensions and directory names, while duplicate paths preserve the extension.</summary>
    [Fact]
    public void AssetAndDuplicateNamesUseRelevantSiblings()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.GetPath("Sky.mat"), "");
        File.WriteAllText(directory.GetPath("Sky 2.png"), "");
        Directory.CreateDirectory(directory.GetPath("Sky 1"));
        File.WriteAllText(directory.GetPath("Sky Copy.mat"), "");
        Directory.CreateDirectory(directory.GetPath("Sky Copy 1"));

        Assert.Equal("Sky 2", PathNamingUtils.GetUniqueAssetName(directory.RootPath, "Sky", ".mat"));
        Assert.Equal("Free", PathNamingUtils.GetUniqueAssetName(directory.GetPath("missing"), "Free", ".mat"));
        Assert.Equal(directory.GetPath("Sky Copy 2.mat"), PathNamingUtils.GetDuplicatePath(directory.GetPath("Sky.mat"), false));
        Assert.Equal(directory.GetPath("Sky 1 Copy"), PathNamingUtils.GetDuplicatePath(directory.GetPath("Sky 1"), true));
    }
}
