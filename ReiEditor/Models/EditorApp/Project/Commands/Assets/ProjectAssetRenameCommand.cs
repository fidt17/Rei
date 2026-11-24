using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed class ProjectAssetRenameCommand(
    IAssetOperationsService operations,
    IAssetRegistry registry,
    IEditorCommandService commands,
    IEditorRefreshService refresh,
    ILogger<ProjectAssetRenameCommand> logger)
    : IProjectAssetRenameCommand, IUndoableEditorCommand<ProjectAssetRenameCommand.Target, ProjectAssetPathChange?>
{
    public sealed record Target(ProjectAssetCommandTarget Asset, string NewName);

    public async Task<ProjectAssetCommandResult> ExecuteAsync(ProjectAssetCommandTarget asset, string newName)
    {
        if (commands.IsBusy) return new ProjectAssetCommandResult(false);
        var change = await commands.ExecuteAsync(this, new Target(asset, newName));
        return change == null
            ? new ProjectAssetCommandResult(false)
            : new ProjectAssetCommandResult(asset.IsDirectory, change.DestinationPath);
    }

    async Task<ProjectAssetPathChange?> IEditorCommand<Target, ProjectAssetPathChange?>.ExecuteAsync(Target target)
    {
        var asset = target.Asset;
        var source = Path.GetFullPath(asset.FullPath);
        if (!PathExtensions.PathExists(source, asset.IsDirectory)) return null;
        logger.Log($"Renaming {source} to {target.NewName}");
        var assetId = registry.TryGetByPath(source, out var info) ? info.Meta.AssetId : null;
        var destinationPath = await operations.RenameAsync(source, target.NewName);
        return destinationPath == null ? null : new ProjectAssetPathChange(asset with { FullPath = source }, destinationPath, assetId);
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, ProjectAssetPathChange?>.CreateHistoryEntry(Target _, ProjectAssetPathChange? change) =>
        change == null ? null : new ProjectAssetPathHistoryEntry(operations, registry, refresh, [change]);
}
