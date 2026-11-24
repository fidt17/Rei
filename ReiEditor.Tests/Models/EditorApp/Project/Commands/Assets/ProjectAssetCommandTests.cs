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
        public (string Path, bool IsDirectory)? DuplicateRequest { get; private set; }

        public Task<string?> RenameAsync(string assetPath, string newName) => throw new NotSupportedException();

        public Task<IReadOnlyList<(string Path, bool IsDirectory)>> DeleteAsync(IReadOnlyList<string> assetPaths) => throw new NotSupportedException();

        /// <summary>Captures duplicate request.</summary>
        public Task DuplicateAsync(string assetPath, bool isDirectory) { ThrowIfConfigured(); DuplicateRequest = (assetPath, isDirectory); return Task.CompletedTask; }

        public Task<string?> MoveAsync(string assetPath, string destinationFolder) => throw new NotSupportedException();
        public bool CanMoveToPath(string assetPath, string targetPath) => throw new NotSupportedException();
        public Task<bool> MoveToPathAsync(string assetPath, string targetPath) => throw new NotSupportedException();

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
    /// Command does not swallow failures raised by operations service.
    /// </summary>
    [Fact]
    public async Task OperationFailurePropagates()
    {
        using var directory = new TemporaryDirectory();
        var expected = new InvalidOperationException("failed");
        var operations = new TestAssetOperationsService { Exception = expected };
        var command = new ProjectAssetDuplicateCommand(operations, new TestLogger<ProjectAssetDuplicateCommand>());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(new ProjectAssetCommandTarget(directory.GetPath("Target"), false)));

        Assert.Same(expected, actual);
    }

}
