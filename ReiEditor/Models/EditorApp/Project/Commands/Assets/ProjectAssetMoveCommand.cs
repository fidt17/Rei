using System;
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
    : IProjectAssetMoveCommand, IUndoableEditorCommand<ProjectAssetMoveCommand.Target, ProjectAssetMoveCommand.Move[]>
{
    public sealed record Target(IReadOnlyList<ProjectAssetCommandTarget> Assets, string DestinationFolder);

    public sealed record Move(ProjectAssetCommandTarget Asset, string DestinationPath, string? AssetId)
    {
        internal string From(bool undo) => undo ? DestinationPath : Asset.FullPath;
        internal string To(bool undo) => From(!undo);
    }

    private sealed class HistoryEntry(IAssetOperationsService operations, IAssetRegistry registry, IEditorRefreshService refresh, Move[] moves) : IEditorHistoryEntry
    {
        private bool _applied = true;

        public bool IsStale => moves.Any(move => !HasSource(move));
        public bool CanUndo => _applied && CanReplay(undo: true);
        public bool CanRedo => !_applied && CanReplay(undo: false);

        public Task UndoAsync() => ReplayAsync(undo: true);
        public Task RedoAsync() => ReplayAsync(undo: false);

        private bool HasSource(Move move)
        {
            var source = move.From(undo: _applied);
            if (!PathExtensions.PathExists(source, move.Asset.IsDirectory)) return false;
            return move.AssetId == null || (registry.TryGetById(move.AssetId, out var info) && info.FullPath.PathEquals(source));
        }

        private bool CanReplay(bool undo) => !IsStale
            && moves.All(move => operations.CanMoveToPath(move.From(undo), move.To(undo)));

        private async Task ReplayAsync(bool undo)
        {
            if (!(undo ? CanUndo : CanRedo)) throw new InvalidOperationException("The asset move cannot be replayed.");

            var completed = new Stack<Move>();
            foreach (var move in undo ? moves.Reverse() : moves)
            {
                if (!await operations.MoveToPathAsync(move.From(undo), move.To(undo)))
                {
                    while (completed.TryPop(out var previous))
                    {
                        if (!await operations.MoveToPathAsync(previous.To(undo), previous.From(undo)))
                            throw new InvalidOperationException("The asset move could not be replayed or rolled back.");
                    }
                    throw new InvalidOperationException("The asset move could not be replayed.");
                }
                completed.Push(move);
            }

            _applied = !undo;
            refresh.NotifyRefreshed();
        }
    }

    public async Task<IReadOnlyList<ProjectAssetCommandResult>> ExecuteAsync(IReadOnlyList<ProjectAssetCommandTarget> assets, string destinationFolder)
    {
        if (commands.IsBusy) return [];
        var moves = await commands.ExecuteAsync(this, new Target(assets, destinationFolder));
        return moves.Select(move => new ProjectAssetCommandResult(move.Asset.IsDirectory, move.DestinationPath)).ToArray();
    }

    async Task<Move[]> IEditorCommand<Target, Move[]>.ExecuteAsync(Target target)
    {
        var moves = new List<Move>();
        foreach (var asset in target.Assets)
        {
            if (!PathExtensions.PathExists(asset.FullPath, asset.IsDirectory)) continue;
            logger.Log($"Moving {asset.FullPath} to {target.DestinationFolder}");
            var source = Path.GetFullPath(asset.FullPath);
            var assetId = registry.TryGetByPath(source, out var info) ? info.Meta.AssetId : null;
            var destinationPath = await operations.MoveAsync(source, target.DestinationFolder);
            if (destinationPath != null) moves.Add(new Move(asset with { FullPath = source }, destinationPath, assetId));
        }
        return moves.ToArray();
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, Move[]>.CreateHistoryEntry(Target _, Move[] moves) =>
        moves.Length == 0 ? null : new HistoryEntry(operations, registry, refresh, moves);
}
