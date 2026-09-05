using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project asset operation guards, target filtering, ordering, and result selection state.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Project")]
public sealed class ProjectAssetOperationsHandlerTests : IDisposable
{
    private sealed class TestDeleteCommand : IProjectAssetDeleteCommand
    {
        public List<ProjectAssetCommandTarget> Targets { get; } = new();

        public Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset)
        {
            Targets.Add(asset);
            return Task.FromResult(new ProjectAssetCommandResult(asset.IsDirectory));
        }
    }

    private sealed class TestDuplicateCommand : IProjectAssetDuplicateCommand
    {
        public List<ProjectAssetCommandTarget> Targets { get; } = new();

        public Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset)
        {
            Targets.Add(asset);
            return Task.FromResult(new ProjectAssetCommandResult(asset.IsDirectory, $"{asset.FullPath}.copy"));
        }
    }

    private sealed class TestMoveCommand : IProjectAssetMoveCommand
    {
        public List<(ProjectAssetCommandTarget Target, string Destination)> Calls { get; } = new();

        public Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string destinationFolder)
        {
            Calls.Add((asset, destinationFolder));
            return Task.FromResult(new ProjectAssetCommandResult(asset.IsDirectory, Path.Combine(destinationFolder, Path.GetFileName(asset.FullPath))));
        }
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();

    /// <summary>
    /// Releases isolated filesystem used for path-boundary tests.
    /// </summary>
    public void Dispose() => _temporaryDirectory.Dispose();

    /// <summary>
    /// Delete and duplicate execute every target in order and report tree impact and last created primary path.
    /// </summary>
    [Fact]
    public async Task TestDeleteAndDuplicateAggregateBatchResults()
    {
        var delete = new TestDeleteCommand();
        var duplicate = new TestDuplicateCommand();
        var handler = new ProjectAssetOperationsHandler(null, null, delete, duplicate, null, null);
        var targets = new[]
        {
            new ProjectAssetCommandTarget(_temporaryDirectory.GetPath("Folder"), true),
            new ProjectAssetCommandTarget(_temporaryDirectory.GetPath("Asset.rei"), false)
        };

        var deleteResult = await handler.DeleteAsync(targets);
        var duplicateResult = await handler.DuplicateAsync(targets);

        Assert.Equal(targets, delete.Targets);
        Assert.True(deleteResult!.AffectsTree);
        Assert.Empty(deleteResult.SelectedAssetPaths);
        Assert.Equal(targets, duplicate.Targets);
        Assert.True(duplicateResult!.AffectsTree);
        Assert.Equal(targets.Select(target => $"{target.FullPath}.copy"), duplicateResult.SelectedAssetPaths);
        Assert.Equal($"{targets[1].FullPath}.copy", duplicateResult.PrimarySelectedAssetPath);
        Assert.Equal(duplicateResult.PrimarySelectedAssetPath, duplicateResult.SelectionAnchorAssetPath);
    }

    /// <summary>
    /// Move ignores targets already in destination and ancestor directories while moving valid targets in order.
    /// </summary>
    [Fact]
    public async Task TestMoveFiltersInvalidTargetsAndReturnsMovedPaths()
    {
        var root = _temporaryDirectory.GetPath("Project");
        var destination = Path.Combine(root, "Destination");
        Directory.CreateDirectory(destination);
        var alreadyThere = new ProjectAssetCommandTarget(Path.Combine(destination, "same.asset"), false);
        var ancestorTarget = new ProjectAssetCommandTarget(root, true);
        var valid = new ProjectAssetCommandTarget(Path.Combine(root, "valid.asset"), false);
        var move = new TestMoveCommand();
        var handler = new ProjectAssetOperationsHandler(null, null, null, null, move, null);

        var result = await handler.MoveAsync(new[] { alreadyThere, ancestorTarget, valid }, destination, root);

        var call = Assert.Single(move.Calls);
        Assert.Equal(valid, call.Target);
        Assert.Equal(destination, call.Destination);
        Assert.Equal(Path.Combine(destination, "valid.asset"), Assert.Single(result!.SelectedAssetPaths));
        Assert.False(result.AffectsTree);
    }

    /// <summary>
    /// Destination with root-name prefix but outside project boundary is rejected without command execution.
    /// </summary>
    [Fact]
    public async Task TestMoveRejectsSiblingDirectorySharingProjectPrefix()
    {
        var root = _temporaryDirectory.GetPath("Project");
        var sibling = _temporaryDirectory.GetPath("Project2");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);
        var move = new TestMoveCommand();
        var handler = new ProjectAssetOperationsHandler(null, null, null, null, move, null);
        var target = new ProjectAssetCommandTarget(Path.Combine(root, "asset.rei"), false);

        var result = await handler.MoveAsync(new[] { target }, sibling, root);

        Assert.Null(result);
        Assert.Empty(move.Calls);
    }

    /// <summary>
    /// Empty root, empty destination, and unavailable command reject move without side effects.
    /// </summary>
    [Fact]
    public async Task TestMoveRejectsMissingInputsAndDependency()
    {
        var target = new ProjectAssetCommandTarget(_temporaryDirectory.GetPath("asset.rei"), false);
        var move = new TestMoveCommand();
        var handler = new ProjectAssetOperationsHandler(null, null, null, null, move, null);

        Assert.Null(await handler.MoveAsync(new[] { target }, "", _temporaryDirectory.RootPath));
        Assert.Null(await handler.MoveAsync(new[] { target }, _temporaryDirectory.RootPath, ""));
        Assert.Null(await new ProjectAssetOperationsHandler(null, null, null, null, null, null)
            .MoveAsync(new[] { target }, _temporaryDirectory.RootPath, _temporaryDirectory.RootPath));
        Assert.Empty(move.Calls);
    }
}
