using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Assets.Search;

/// <summary>
/// Verifies recursive asset search, filtering, ordering, and extension selection.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class AssetSearchServiceTests
{
    /// <summary>
    /// Search trims query, ignores case, and orders matching directories before files by name.
    /// </summary>
    [Fact]
    public async Task SearchMatchesTrimmedQueryAndOrdersDirectoriesBeforeFiles()
    {
        using var fixture = new TemporaryProjectFixture();
        var project = fixture.Resources.GetProjectPath();
        var directory = Path.Combine(project, "Alpha Match");
        Directory.CreateDirectory(directory);
        var firstFile = Path.Combine(project, "alpha match.mat");
        var secondFile = Path.Combine(project, "Zeta MATCH.scene");
        await fixture.Resources.Write("{}", firstFile);
        await fixture.Resources.Write("{}", secondFile);

        var results = new AssetSearchService(fixture.Resources).Search("  MaTcH  ");

        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsDirectory);
        Assert.Equal("Alpha Match", results[0].Name);
        Assert.Equal(new[] { "alpha match.mat", "Zeta MATCH.scene" }, results.Skip(1).Select(result => result.Name));
    }

    /// <summary>
    /// Hidden files are omitted and hidden directories are not traversed.
    /// </summary>
    [Fact]
    public async Task SearchSkipsHiddenFilesAndDirectoryTrees()
    {
        using var fixture = new TemporaryProjectFixture();
        var scripts = fixture.Resources.GetScriptsPath();
        await fixture.Resources.Write("visible", Path.Combine(scripts, "VisibleTarget.cpp"));
        await fixture.Resources.Write("hidden", Path.Combine(scripts, "VisibleTarget.cpp.meta"));
        await fixture.Resources.Write("hidden", Path.Combine(scripts, "Target.vcxproj"));
        await fixture.Resources.Write("hidden", Path.Combine(scripts, "bin", "HiddenTarget.cpp"));
        await fixture.Resources.Write("hidden", Path.Combine(scripts, "crash_reports", "CrashTarget.cpp"));

        var results = new AssetSearchService(fixture.Resources).Search("target");

        var result = Assert.Single(results);
        Assert.Equal("VisibleTarget.cpp", result.Name);
        Assert.False(result.IsDirectory);
    }

    /// <summary>
    /// Extension search keeps matching directories and files while excluding files with other extensions.
    /// </summary>
    [Fact]
    public async Task SearchByExtensionsKeepsDirectoriesAndMatchingFiles()
    {
        using var fixture = new TemporaryProjectFixture();
        var project = fixture.Resources.GetProjectPath();
        Directory.CreateDirectory(Path.Combine(project, "Target Folder"));
        await fixture.Resources.Write("{}", Path.Combine(project, "Target.MAT"));
        await fixture.Resources.Write("{}", Path.Combine(project, "Target.scene"));

        var results = new AssetSearchService(fixture.Resources).SearchByExtensions("target", new[] { FileExtensions.MATERIAL });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.IsDirectory && result.Name == "Target Folder");
        Assert.Contains(results, result => !result.IsDirectory && result.Name == "Target.MAT");
        Assert.DoesNotContain(results, result => result.Name == "Target.scene");
    }

    /// <summary>
    /// Blank query, empty extensions, and missing project root return empty results.
    /// </summary>
    [Fact]
    public void InvalidInputsAndMissingRootReturnNoResults()
    {
        using var fixture = new TemporaryProjectFixture();
        var service = new AssetSearchService(fixture.Resources);

        Assert.Empty(service.Search(" \r\n "));
        Assert.Empty(service.SearchByExtensions("target", Array.Empty<string>()));
        Directory.Delete(fixture.Directory.RootPath, recursive: true);
        Assert.Empty(service.Search("target"));
    }
}
