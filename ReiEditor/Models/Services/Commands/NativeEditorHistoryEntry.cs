using System;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Commands;

public sealed class NativeEditorHistoryEntry(ulong actionId, IEngineNativeAccess engine) : IEditorHistoryEntry
{
    private readonly object? _instanceToken = engine.InstanceToken;

    internal bool IsStale => _instanceToken == null || !ReferenceEquals(_instanceToken, engine.InstanceToken);
    public bool CanUndo => !IsStale;
    public bool CanRedo => CanUndo;

    public Task UndoAsync() => ReplayAsync(undo: true);
    public Task RedoAsync() => ReplayAsync(undo: false);

    private Task ReplayAsync(bool undo) => Task.Run(() =>
    {
        var applied = false;
        if (!engine.TryInvoke(api =>
        {
            // Check under the same lock that protects the native call from engine replacement.
            if (!CanUndo) throw new InvalidOperationException("The native action belongs to a previous engine instance.");
            applied = undo ? api.UndoEditorAction(actionId) : api.RedoEditorAction(actionId);
        }))
            throw new InvalidOperationException("The native engine is unavailable.");
        if (!applied) throw new InvalidOperationException($"Could not {(undo ? "undo" : "redo")} native Editor action {actionId}.");
    });
}
