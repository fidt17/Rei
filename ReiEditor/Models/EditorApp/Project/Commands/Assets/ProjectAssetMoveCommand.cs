using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed class ProjectAssetMoveCommand(
    IAssetOperationsService operations,
    IAssetRegistry registry,
    IEditorCommandService commands,
    IEditorRefreshService refresh,
    ILogger<ProjectAssetMoveCommand> logger)
    : IProjectAssetMoveCommand, IUndoableEditorCommand<ProjectAssetMoveCommand.Target, ProjectAssetPathChange[]>
{
    public sealed record Target(IReadOnlyList<ProjectAssetCommandTarget> Assets, string DestinationFolder);

    public async Task<IReadOnlyList<ProjectAssetCommandResult>> ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets, string destinationFolder)
    {
        if (commands.IsBusy) return [];
        var moves = await commands.ExecuteAsync(this, new Target(assets, destinationFolder));
        return moves.Select(move => new ProjectAssetCommandResult(move.Asset.IsDirectory, move.DestinationPath)).ToArray();
    }

    async Task<ProjectAssetPathChange[]> IEditorCommand<Target, ProjectAssetPathChange[]>.ExecuteAsync(Target target)
    {
        var moves = new List<ProjectAssetPathChange>();
        foreach (var asset in target.Assets)
        {
            if (!PathExtensions.PathExists(asset.FullPath, asset.IsDirectory)) continue;
            logger.Log($"Moving {asset.FullPath} to {target.DestinationFolder}");
            var source = Path.GetFullPath(asset.FullPath);
            var assetId = registry.TryGetByPath(source, out var info) ? info.Meta.AssetId : null;
            var destinationPath = await operations.MoveAsync(source, target.DestinationFolder);
            if (destinationPath != null) moves.Add(new ProjectAssetPathChange(asset with { FullPath = source }, destinationPath, assetId));
        }
        return moves.ToArray();
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, ProjectAssetPathChange[]>.CreateHistoryEntry(Target _, ProjectAssetPathChange[] moves) =>
        moves.Length == 0 ? null : new ProjectAssetPathHistoryEntry(operations, registry, refresh, moves);
}
