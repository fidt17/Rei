using ReiEditor.Models.Services.Build.Solution;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Services.Build.Solution;

/// <summary>Verifies in-memory source change tracking against isolated script trees.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class SourceTrackerTests : IDisposable
{
    private readonly TemporaryProjectFixture _project = new();

    /// <summary>First scan reports sources and unchanged second scan becomes a no-op.</summary>
    [Fact]
    public async Task TestFirstScanDetectsSourcesThenSnapshotBecomesStable()
    {
        await WriteScript("Player.cpp", "int player = 1;");
        var tracker = new SourceTracker(_project.Resources);

        Assert.True(await tracker.ChangedOrNewSourcesExist());
        Assert.False(await tracker.ChangedOrNewSourcesExist());
    }

    /// <summary>Same-length content replacement still reports source change.</summary>
    [Fact]
    public async Task TestSameLengthContentChangeIsDetected()
    {
        var path = await WriteScript("State.h", "AAAA");
        var tracker = new SourceTracker(_project.Resources);
        await tracker.ChangedOrNewSourcesExist();

        await File.WriteAllTextAsync(path, "BBBB");

        Assert.True(await tracker.ChangedOrNewSourcesExist());
    }

    /// <summary>New nested C++ and header files are tracked while unrelated extensions are ignored.</summary>
    [Fact]
    public async Task TestNestedSourcesTrackedAndUnrelatedFilesIgnored()
    {
        Directory.CreateDirectory(_project.Resources.GetScriptsPath("Nested"));
        await File.WriteAllTextAsync(_project.Resources.GetScriptsPath("Notes.txt"), "one");
        var tracker = new SourceTracker(_project.Resources);

        Assert.False(await tracker.ChangedOrNewSourcesExist());
        await WriteScript(Path.Combine("Nested", "Added.cpp"), "int added;");
        Assert.True(await tracker.ChangedOrNewSourcesExist());
        Assert.False(await tracker.ChangedOrNewSourcesExist());
    }

    /// <summary>Writes script content beneath isolated project scripts root.</summary>
    private async Task<string> WriteScript(string relativePath, string content)
    {
        var path = _project.Resources.GetScriptsPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    /// <summary>Deletes isolated project tree.</summary>
    public void Dispose() => _project.Dispose();
}
