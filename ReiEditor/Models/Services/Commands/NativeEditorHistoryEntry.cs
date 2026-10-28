using System;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Commands;

public sealed class NativeEditorHistoryEntry(ulong actionId, IEngineNativeAccess engine) : IEditorHistoryEntry
{
    // Native action validity is checked by the engine when replayed.
    public bool CanUndo => true;
    public bool CanRedo => true;

    public Task UndoAsync() => ReplayAsync(undo: true);
    public Task RedoAsync() => ReplayAsync(undo: false);

    private Task ReplayAsync(bool undo) => Task.Run(() =>
    {
        var applied = false;
        if (!engine.TryInvoke(api => applied = undo ? api.UndoEditorAction(actionId) : api.RedoEditorAction(actionId)))
            throw new InvalidOperationException("The native engine is unavailable.");
        if (!applied) throw new InvalidOperationException($"Could not {(undo ? "undo" : "redo")} native Editor action {actionId}.");
    });
}
