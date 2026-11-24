using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

internal sealed record ProjectAssetPathChange(ProjectAssetCommandTarget Asset, string DestinationPath, string? AssetId)
{
    internal string From(bool undo) => undo ? DestinationPath : Asset.FullPath;
    internal string To(bool undo) => From(!undo);
}

internal sealed class ProjectAssetPathHistoryEntry(
    IAssetOperationsService operations,
    IAssetRegistry registry,
    IEditorRefreshService refresh,
    ProjectAssetPathChange[] changes) : IEditorHistoryEntry
{
    private bool _applied = true;

    public bool IsStale => changes.Any(change => !HasSource(change));
    public bool CanUndo => _applied && CanReplay(undo: true);
    public bool CanRedo => !_applied && CanReplay(undo: false);

    public Task UndoAsync() => ReplayAsync(undo: true);
    public Task RedoAsync() => ReplayAsync(undo: false);

    private bool HasSource(ProjectAssetPathChange change)
    {
        var source = change.From(undo: _applied);
        if (!PathExtensions.PathExists(source, change.Asset.IsDirectory)) return false;
        return change.AssetId == null || (registry.TryGetById(change.AssetId, out var info) && info.FullPath.PathEquals(source));
    }

    private bool CanReplay(bool undo) => !IsStale
        && changes.All(change => operations.CanMoveToPath(change.From(undo), change.To(undo)));

    private async Task ReplayAsync(bool undo)
    {
        if (!(undo ? CanUndo : CanRedo)) throw new InvalidOperationException("The asset path change cannot be replayed.");

        var completed = new Stack<ProjectAssetPathChange>();
        foreach (var change in undo ? changes.Reverse() : changes)
        {
            if (!await operations.MoveToPathAsync(change.From(undo), change.To(undo)))
            {
                while (completed.TryPop(out var previous))
                {
                    if (!await operations.MoveToPathAsync(previous.To(undo), previous.From(undo)))
                        throw new InvalidOperationException("The asset path change could not be replayed or rolled back.");
                }
                throw new InvalidOperationException("The asset path change could not be replayed.");
            }
            completed.Push(change);
        }

        _applied = !undo;
        refresh.NotifyRefreshed();
    }
}
