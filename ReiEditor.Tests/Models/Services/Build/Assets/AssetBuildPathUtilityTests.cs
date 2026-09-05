using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Build.Assets;

/// <summary>Verifies which registered filesystem paths participate in asset packing.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class AssetBuildPathUtilityTests
{
    /// <summary>Runtime asset extensions are accepted while source and project metadata are excluded.</summary>
    [Theory]
    [InlineData("texture.png", true)]
    [InlineData("scene.scene", true)]
    [InlineData("asset.meta", false)]
    [InlineData("source.h", false)]
    [InlineData("source.cpp", false)]
    [InlineData("game.vcxproj", false)]
    [InlineData("game.vcxproj.user", false)]
    [InlineData("game.VCXPROJ.USER", false)]
    [InlineData("game.sln", false)]
    [InlineData("asset.META", false)]
    [InlineData("source.H", false)]
    [InlineData("source.CPP", false)]
    [InlineData("game.VCXPROJ", false)]
    [InlineData("game.SLN", false)]
    [InlineData("settings.user", true)]
    [InlineData("game.vcxproj.user.bak", true)]
    public void TestShouldBuildPathFiltersExtensions(string fileName, bool expected)
    {
        using var directory = new TemporaryDirectory();

        Assert.Equal(expected, AssetBuildPathUtility.ShouldBuildPath(directory.GetPath("Project", fileName)));
    }

    /// <summary>Paths under script build and crash-report trees are excluded regardless of directory casing.</summary>
    [Theory]
    [InlineData("bin")]
    [InlineData("BIN")]
    [InlineData("crash_reports")]
    public void TestShouldBuildPathExcludesHiddenDirectoryTrees(string hiddenDirectory)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("Project", "Scripts", hiddenDirectory, "nested", "texture.png");

        Assert.False(AssetBuildPathUtility.ShouldBuildPath(path));
    }

    /// <summary>A compound project-user suffix excludes only files, not parent directories with the same suffix.</summary>
    [Fact]
    public void TestShouldBuildPathAllowsAssetUnderDirectoryEndingWithProjectUserSuffix()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("Project", "folder.vcxproj.user", "nested", "texture.png");

        Assert.True(AssetBuildPathUtility.ShouldBuildPath(path));
    }

    /// <summary>Blank paths are rejected without filesystem access.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TestShouldBuildPathRejectsBlankPath(string? path)
    {
        Assert.False(AssetBuildPathUtility.ShouldBuildPath(path!));
    }
}
