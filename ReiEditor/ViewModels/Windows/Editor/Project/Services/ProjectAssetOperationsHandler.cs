using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IOPath = System.IO.Path;
using Avalonia.Platform.Storage;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.Services.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Services;

public sealed record ProjectAssetBatchCommandResult(
    bool AffectsTree,
    IReadOnlyCollection<string> SelectedAssetPaths,
    string? PrimarySelectedAssetPath = null,
    string? SelectionAnchorAssetPath = null);

public class ProjectAssetOperationsHandler
{
    private readonly IStorageProvider? _storageProvider;
    private readonly IAssetOperationsService? _assetOperationsService;
    private readonly IProjectAssetDeleteCommand? _projectAssetDeleteCommand;
    private readonly IProjectAssetDuplicateCommand? _projectAssetDuplicateCommand;
    private readonly IProjectAssetMoveCommand? _projectAssetMoveCommand;
    private readonly IProjectAssetRenameCommand? _projectAssetRenameCommand;

    public ProjectAssetOperationsHandler(
        IStorageProvider? storageProvider,
        IAssetOperationsService? assetOperationsService,
        IProjectAssetDeleteCommand? projectAssetDeleteCommand,
        IProjectAssetDuplicateCommand? projectAssetDuplicateCommand,
        IProjectAssetMoveCommand? projectAssetMoveCommand,
        IProjectAssetRenameCommand? projectAssetRenameCommand)
    {
        _storageProvider = storageProvider;
        _assetOperationsService = assetOperationsService;
        _projectAssetDeleteCommand = projectAssetDeleteCommand;
        _projectAssetDuplicateCommand = projectAssetDuplicateCommand;
        _projectAssetMoveCommand = projectAssetMoveCommand;
        _projectAssetRenameCommand = projectAssetRenameCommand;
    }

    public Task<ProjectAssetCommandResult>? RenameAsync(ProjectAssetItemViewModel item, string newName)
    {
        return _projectAssetRenameCommand?.ExecuteAsync(CreateCommandTarget(item), newName);
    }

    public async Task<ProjectAssetBatchCommandResult?> DeleteAsync(IReadOnlyList<ProjectAssetCommandTarget> targets)
    {
        if (_projectAssetDeleteCommand == null) return null;

        foreach (var target in targets)
        {
            await _projectAssetDeleteCommand.ExecuteAsync(target);
        }

        return new ProjectAssetBatchCommandResult(targets.Any(target => target.IsDirectory), System.Array.Empty<string>());
    }

    public async Task<ProjectAssetBatchCommandResult?> MoveAsync(IReadOnlyList<ProjectAssetCommandTarget> targets, string projectRootPath)
    {
        if (_storageProvider == null || _projectAssetMoveCommand == null) return null;
        if (string.IsNullOrWhiteSpace(projectRootPath)) return null;

        var selectedFolder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Move to Directory",
            AllowMultiple = false
        });

        if (selectedFolder.Count == 0) return null;

        var destFolderPath = selectedFolder[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destFolderPath)) return null;

        var fullDestFolderPath = IOPath.GetFullPath(destFolderPath);
        return await MoveAsync(targets, fullDestFolderPath, projectRootPath);
    }

    public async Task<ProjectAssetBatchCommandResult?> MoveAsync(IReadOnlyList<ProjectAssetCommandTarget> targets, string destinationFolderPath, string projectRootPath)
    {
        if (_projectAssetMoveCommand == null) return null;
        if (string.IsNullOrWhiteSpace(projectRootPath)) return null;
        if (string.IsNullOrWhiteSpace(destinationFolderPath)) return null;

        var fullDestFolderPath = IOPath.GetFullPath(destinationFolderPath);
        var fullRootPath = IOPath.GetFullPath(projectRootPath);
        if (!fullDestFolderPath.StartsWith(fullRootPath, System.StringComparison.OrdinalIgnoreCase)) return null;

        var moveTargets = targets
            .Where(target => ShouldMoveTarget(target, fullDestFolderPath))
            .ToList();
        if (moveTargets.Count == 0) return null;

        var movedAssetPaths = new List<string>();
        foreach (var target in moveTargets)
        {
            var result = await _projectAssetMoveCommand.ExecuteAsync(target, fullDestFolderPath);
            if (!string.IsNullOrWhiteSpace(result.SelectedAssetPath))
            {
                movedAssetPaths.Add(result.SelectedAssetPath);
            }
        }

        var primarySelectedPath = movedAssetPaths.LastOrDefault();
        return new ProjectAssetBatchCommandResult(moveTargets.Any(target => target.IsDirectory), movedAssetPaths, primarySelectedPath, primarySelectedPath);
    }

    public async Task<ProjectAssetBatchCommandResult?> DuplicateAsync(IReadOnlyList<ProjectAssetCommandTarget> targets)
    {
        if (_projectAssetDuplicateCommand == null) return null;

        var duplicatedAssetPaths = new List<string>();
        foreach (var target in targets)
        {
            var result = await _projectAssetDuplicateCommand.ExecuteAsync(target);
            if (!string.IsNullOrWhiteSpace(result.SelectedAssetPath))
            {
                duplicatedAssetPaths.Add(result.SelectedAssetPath);
            }
        }

        var primarySelectedPath = duplicatedAssetPaths.LastOrDefault();
        return new ProjectAssetBatchCommandResult(targets.Any(target => target.IsDirectory), duplicatedAssetPaths, primarySelectedPath, primarySelectedPath);
    }

    public Task ImportExternalPathsAsync(IReadOnlyCollection<string> paths, string targetDirectory)
    {
        if (_assetOperationsService == null) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return Task.CompletedTask;
        if (paths.Count == 0) return Task.CompletedTask;

        return _assetOperationsService.ImportExternalAssets(paths, targetDirectory);
    }

    public Task CreateFolderAsync(string baseDirectory)
    {
        if (_assetOperationsService == null) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory)) return Task.CompletedTask;

        return _assetOperationsService.CreateFolderAsync(baseDirectory, "New Folder");
    }

    private static ProjectAssetCommandTarget CreateCommandTarget(ProjectAssetItemViewModel item)
    {
        return new ProjectAssetCommandTarget(item.FullPath, item.IsDirectory);
    }

    private static bool ShouldMoveTarget(ProjectAssetCommandTarget target, string destinationFolderPath)
    {
        var sourceParentDirectory = IOPath.GetDirectoryName(target.FullPath);
        if (!string.IsNullOrWhiteSpace(sourceParentDirectory) &&
            string.Equals(IOPath.GetFullPath(sourceParentDirectory), destinationFolderPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!target.IsDirectory) return true;
        return !IsSameOrDescendantPath(destinationFolderPath, target.FullPath);
    }

    private static bool IsSameOrDescendantPath(string path, string rootPath)
    {
        var normalizedPath = NormalizeDirectoryPath(path);
        var normalizedRootPath = NormalizeDirectoryPath(rootPath);
        return normalizedPath.StartsWith(normalizedRootPath, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = IOPath.GetFullPath(path)
            .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
        return fullPath + IOPath.DirectorySeparatorChar;
    }
}
