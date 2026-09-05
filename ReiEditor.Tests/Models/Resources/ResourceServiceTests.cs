using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Resources;

/// <summary>
/// Verifies resource persistence using real files inside an isolated temporary project.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Resources")]
public sealed class ResourceServiceTests
{
    /// <summary>
    /// Writing a resource creates missing parent directories and preserves JSON data for subsequent loading.
    /// </summary>
    [Fact]
    public async Task WriteCreatesNestedDirectoryAndLoadReadsStoredJson()
    {
        using var fixture = new TemporaryProjectFixture();
        var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "Resources", "settings.json"));
        var path = fixture.Directory.GetPath("Project", "Settings", "scene.json");

        var written = await fixture.Resources.Write(source, path);
        var loaded = await fixture.Resources.Load<JObject>(path);

        Assert.True(written);
        Assert.True(fixture.Resources.Exists(path));
        Assert.Equal(source, await File.ReadAllTextAsync(path));
        Assert.Equal("Test scene", loaded.Value<string>("name"));
        Assert.Equal(3, loaded.Value<int>("entityCount"));
        Assert.DoesNotContain(fixture.ResourceLogger.Entries, entry => entry.Level == LogLevelEnum.Error);
    }

    /// <summary>
    /// Root, project, and scripts path helpers combine segments under isolated project root.
    /// </summary>
    [Fact]
    public void PathHelpersResolveExpectedProjectLocations()
    {
        using var fixture = new TemporaryProjectFixture();

        Assert.Equal(fixture.Directory.GetPath("Engine", "Shaders"), fixture.Resources.GetRootPath("Engine", "Shaders"));
        Assert.Equal(fixture.Directory.GetPath("Project", "Assets"), fixture.Resources.GetProjectPath("Assets"));
        Assert.Equal(fixture.Directory.GetPath("Project", "Scripts", "Behaviours"), fixture.Resources.GetScriptsPath("Behaviours"));
    }

    /// <summary>
    /// Extension enumeration finds matching files recursively under resource root.
    /// </summary>
    [Fact]
    public async Task GetAllWithExtensionEnumeratesNestedMatches()
    {
        using var fixture = new TemporaryProjectFixture();
        var first = fixture.Directory.GetPath("Project", "first.mat");
        var second = fixture.Directory.GetPath("Engine", "Nested", "second.mat");
        await fixture.Resources.Write("first", first);
        await fixture.Resources.Write("second", second);
        await fixture.Resources.Write("other", fixture.Directory.GetPath("Project", "other.scene"));

        var matches = fixture.Resources.GetAllWithExtension(".mat").OrderBy(path => path).ToList();

        Assert.Equal(new[] { second, first }.OrderBy(path => path), matches);
    }

    /// <summary>
    /// Recursive copy creates target tree, preserves empty directories, and overwrites existing files.
    /// </summary>
    [Fact]
    public async Task CopyFilesRecursivelyCopiesTreeAndOverwritesTargets()
    {
        using var fixture = new TemporaryProjectFixture();
        var source = fixture.Directory.GetPath("Source");
        var target = fixture.Directory.GetPath("Target");
        Directory.CreateDirectory(Path.Combine(source, "Empty"));
        await fixture.Resources.Write("new", Path.Combine(source, "Nested", "asset.txt"));
        await fixture.Resources.Write("old", Path.Combine(target, "Nested", "asset.txt"));

        fixture.Resources.CopyFilesRecursively(source, target);

        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(target, "Nested", "asset.txt")));
        Assert.True(Directory.Exists(Path.Combine(target, "Empty")));
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(source, "Nested", "asset.txt")));
    }

    /// <summary>
    /// Recursive move transfers nested files with overwrite and leaves source directory structure intact.
    /// </summary>
    [Fact]
    public async Task MoveFilesRecursivelyMovesFilesAndKeepsSourceDirectories()
    {
        using var fixture = new TemporaryProjectFixture();
        var source = fixture.Directory.GetPath("Source");
        var target = fixture.Directory.GetPath("Target");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.Combine(source, "Empty"));
        var sourceFile = Path.Combine(source, "Nested", "asset.txt");
        var targetFile = Path.Combine(target, "Nested", "asset.txt");
        await fixture.Resources.Write("new", sourceFile);
        await fixture.Resources.Write("old", targetFile);

        fixture.Resources.MoveFilesRecursively(source, target);

        Assert.False(File.Exists(sourceFile));
        Assert.Equal("new", await File.ReadAllTextAsync(targetFile));
        Assert.True(Directory.Exists(Path.Combine(source, "Nested")));
        Assert.True(Directory.Exists(Path.Combine(target, "Empty")));
    }

    /// <summary>
    /// Load propagates missing and malformed resource failures.
    /// </summary>
    [Fact]
    public async Task LoadPropagatesMissingAndMalformedResourceFailures()
    {
        using var fixture = new TemporaryProjectFixture();
        var missing = fixture.Directory.GetPath("missing.json");
        var malformed = fixture.Directory.GetPath("malformed.json");
        await File.WriteAllTextAsync(malformed, "{");

        await Assert.ThrowsAsync<Exception>(() => fixture.Resources.Load<JObject>(missing));
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Resources.Load<JObject>(malformed));
    }

    /// <summary>
    /// TryLoad returns default and logs exceptions for missing and malformed resources.
    /// </summary>
    [Fact]
    public async Task TryLoadReturnsDefaultAndLogsResourceFailures()
    {
        using var fixture = new TemporaryProjectFixture();
        var malformed = fixture.Directory.GetPath("malformed.json");
        await File.WriteAllTextAsync(malformed, "{");

        var missingResult = await fixture.Resources.TryLoad<JObject>(fixture.Directory.GetPath("missing.json"));
        var malformedResult = await fixture.Resources.TryLoad<JObject>(malformed);

        Assert.Null(missingResult);
        Assert.Null(malformedResult);
        Assert.Equal(2, fixture.ResourceLogger.Entries.Count(entry => entry.Level == LogLevelEnum.Error));
    }

    /// <summary>
    /// Write returns false and logs when destination is an existing directory.
    /// </summary>
    [Fact]
    public async Task WriteFailureReturnsFalseAndLogsException()
    {
        using var fixture = new TemporaryProjectFixture();
        var directoryPath = fixture.Directory.GetPath("Destination");
        Directory.CreateDirectory(directoryPath);

        var written = await fixture.Resources.Write("content", directoryPath);

        Assert.False(written);
        Assert.Single(fixture.ResourceLogger.Entries, entry => entry.Level == LogLevelEnum.Error);
    }

    /// <summary>
    /// Write creates parent when filename text also appears as directory segment.
    /// </summary>
    [Fact]
    public async Task WriteHandlesRepeatedFilenameSegment()
    {
        using var fixture = new TemporaryProjectFixture();
        var path = fixture.Directory.GetPath("settings.json", "settings.json");

        var written = await fixture.Resources.Write("content", path);

        Assert.True(written);
        Assert.Equal("content", await File.ReadAllTextAsync(path));
    }
}
