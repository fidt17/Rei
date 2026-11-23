using System;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Commands;

public sealed class NativeEditorHistoryEntry(ulong actionId, IEngineNativeAccess engine) : IEditorHistoryEntry
{
    private readonly object? _context = engine.EditorActionContext;

    internal bool IsStale => _context == null || !ReferenceEquals(_context, engine.EditorActionContext);
    public bool CanUndo => !IsStale;
    public bool CanRedo => CanUndo;

    public Task UndoAsync() => TryReplayAsync(undo: true);
    public Task RedoAsync() => TryReplayAsync(undo: false);

    internal void Discard()
    {
        if (IsStale) return;
        engine.TryInvoke(api =>
        {
            if (!IsStale) api.DiscardEditorAction(actionId);
        });
    }

    internal Task<bool> TryReplayAsync(bool undo) => Task.Run(() =>
    {
        var result = EditorActionReplayResult.Failed;
        if (!engine.TryInvoke(api =>
        {
            // Check under the same lock as scene loading and engine replacement.
            if (!CanUndo) throw new InvalidOperationException("The native action belongs to a previous engine or scene.");
            result = undo ? api.UndoEditorAction(actionId) : api.RedoEditorAction(actionId);
        }))
            throw new InvalidOperationException("The native engine is unavailable.");
        if (result == EditorActionReplayResult.TargetMissing) return false;
        if (result != EditorActionReplayResult.Applied)
            throw new InvalidOperationException($"Could not {(undo ? "undo" : "redo")} native Editor action {actionId}.");
        return true;
    });
}
