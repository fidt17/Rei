using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.Project.Commands.Assets;

/// <summary>
/// Verifies project asset commands forward exact targets and report navigation results.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class ProjectAssetCommandTests
{
    /// <summary>
    /// Captures operation arguments and optionally raises configured failure.
    /// </summary>
    private sealed class TestAssetOperationsService : IAssetOperationsService
    {
        public Exception? Exception { get; init; }
        public (string Path, string Name)? RenameRequest { get; private set; }
        public (string Path, bool IsDirectory)? DeleteRequest { get; private set; }
        public (string Path, bool IsDirectory)? DuplicateRequest { get; private set; }
        public (string Path, string Destination)? MoveRequest { get; private set; }

        /// <summary>Captures rename request.</summary>
        public Task RenameAsync(string assetPath, string newName) { ThrowIfConfigured(); RenameRequest = (assetPath, newName); return Task.CompletedTask; }

        /// <summary>Captures delete request.</summary>
        public Task DeleteAsync(string assetPath, bool isDirectory) { ThrowIfConfigured(); DeleteRequest = (assetPath, isDirectory); return Task.CompletedTask; }

        /// <summary>Captures duplicate request.</summary>
        public Task DuplicateAsync(string assetPath, bool isDirectory) { ThrowIfConfigured(); DuplicateRequest = (assetPath, isDirectory); return Task.CompletedTask; }

        /// <summary>Captures move request.</summary>
        public Task MoveAsync(string assetPath, string destinationFolder) { ThrowIfConfigured(); MoveRequest = (assetPath, destinationFolder); return Task.CompletedTask; }

        /// <summary>Rejects unused external import.</summary>
        public Task ImportExternalAssets(IEnumerable<string> sourcePaths, string targetFolder) => throw new NotSupportedException();

        /// <summary>Rejects unused folder creation.</summary>
        public Task CreateFolderAsync(string parentDirectory, string folderName) => throw new NotSupportedException();

        /// <summary>Raises configured error before recording request.</summary>
        private void ThrowIfConfigured()
        {
            if (Exception != null) throw Exception;
        }
    }


    /// <summary>
    /// Rename forwards original path and returns trimmed destination path.
    /// </summary>
    [Fact]
    public async Task RenameForwardsTargetAndReturnsTrimmedPath()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.GetPath("Old.scene");
        var operations = new TestAssetOperationsService();
        var command = new ProjectAssetRenameCommand(operations, new TestLogger<ProjectAssetRenameCommand>());

        var result = await command.ExecuteAsync(new ProjectAssetCommandTarget(source, false), "  New.scene  ");

        Assert.Equal((source, "  New.scene  "), operations.RenameRequest);
        Assert.False(result.AffectsTree);
        Assert.Equal(directory.GetPath("New.scene"), result.SelectedAssetPath);
    }

    /// <summary>
    /// Duplicate computes next suffix from real collisions and forwards directory flag.
    /// </summary>
    [Fact]
    public async Task DuplicateReturnsNextCollisionPath()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.GetPath("Folder");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(directory.GetPath("Folder Copy"));
        var operations = new TestAssetOperationsService();
        var command = new ProjectAssetDuplicateCommand(operations, new TestLogger<ProjectAssetDuplicateCommand>());

        var result = await command.ExecuteAsync(new ProjectAssetCommandTarget(source, true));

        Assert.Equal((source, true), operations.DuplicateRequest);
        Assert.True(result.AffectsTree);
        Assert.Equal(directory.GetPath("Folder Copy 1"), result.SelectedAssetPath);
    }

    /// <summary>
    /// Move forwards destination and reports source filename inside it.
    /// </summary>
    [Fact]
    public async Task MoveForwardsDestinationAndReturnsMovedPath()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.GetPath("Source.scene");
        var destination = directory.GetPath("Target");
        var operations = new TestAssetOperationsService();
        var command = new ProjectAssetMoveCommand(operations, new TestLogger<ProjectAssetMoveCommand>());

        var result = await command.ExecuteAsync(new ProjectAssetCommandTarget(source, false), destination);

        Assert.Equal((source, destination), operations.MoveRequest);
        Assert.False(result.AffectsTree);
        Assert.Equal(Path.Combine(destination, "Source.scene"), result.SelectedAssetPath);
    }

    /// <summary>
    /// Delete forwards path and directory flag and reports tree impact only for directories.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteReportsDirectoryTreeImpact(bool isDirectory)
    {
        using var directory = new TemporaryDirectory();
        var source = directory.GetPath("Target");
        var operations = new TestAssetOperationsService();
        var command = new ProjectAssetDeleteCommand(operations, new TestLogger<ProjectAssetDeleteCommand>());

        var result = await command.ExecuteAsync(new ProjectAssetCommandTarget(source, isDirectory));

        Assert.Equal((source, isDirectory), operations.DeleteRequest);
        Assert.Equal(isDirectory, result.AffectsTree);
        Assert.Null(result.SelectedAssetPath);
    }

    /// <summary>
    /// Command does not swallow failures raised by operations service.
    /// </summary>
    [Fact]
    public async Task OperationFailurePropagates()
    {
        using var directory = new TemporaryDirectory();
        var expected = new InvalidOperationException("failed");
        var operations = new TestAssetOperationsService { Exception = expected };
        var command = new ProjectAssetDeleteCommand(operations, new TestLogger<ProjectAssetDeleteCommand>());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(new ProjectAssetCommandTarget(directory.GetPath("Target"), false)));

        Assert.Same(expected, actual);
    }

}
