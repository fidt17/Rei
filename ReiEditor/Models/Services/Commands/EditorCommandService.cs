using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;

namespace ReiEditor.Models.Services.Commands;

public sealed class EditorCommandService : IEditorCommandService, IDisposable
{
    public bool IsBusy { get { lock (_gate) return _isBusy; } }
    public bool CanUndo { get { lock (_gate) return !_isBusy && PeekCurrentEntry(_undo)?.CanUndo == true; } }
    public bool CanRedo { get { lock (_gate) return !_isBusy && PeekCurrentEntry(_redo)?.CanRedo == true; } }

    private readonly object _gate = new();
    private readonly Stack<IEditorHistoryEntry> _undo = new();
    private readonly Stack<IEditorHistoryEntry> _redo = new();
    private bool _isBusy;
    private bool _disposed;

    public async Task<TResult> ExecuteAsync<TArgs, TResult>(IEditorCommand<TArgs, TResult> command, TArgs args)
    {
        BeginOperation();
        try
        {
            var result = await command.ExecuteAsync(args);
            lock (_gate)
            {
                if (_disposed) return result;
                var entry = (command as IUndoableEditorCommand<TArgs, TResult>)?.CreateHistoryEntry(args, result);
                if (entry != null) AddHistoryEntry(entry);
            }
            return result;
        }
        finally { EndOperation(); }
    }

    public void RecordApplied(IEditorHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isBusy) throw new InvalidOperationException("An Editor command operation is already running.");
            AddHistoryEntry(entry);
        }
    }

    public Task UndoAsync() => TraverseAsync(_undo, _redo, undo: true);
    public Task RedoAsync() => TraverseAsync(_redo, _undo, undo: false);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _undo.Clear();
            _redo.Clear();
        }
    }

    private async Task TraverseAsync(Stack<IEditorHistoryEntry> source, Stack<IEditorHistoryEntry> destination, bool undo)
    {
        BeginOperation();
        try
        {
            while (true)
            {
                IEditorHistoryEntry? entry;
                lock (_gate) entry = PeekCurrentEntry(source);
                if (entry == null) return;

                var applied = true;
                if (entry is NativeEditorHistoryEntry native) applied = await native.TryReplayAsync(undo);
                else if (undo) await entry.UndoAsync();
                else await entry.RedoAsync();

                lock (_gate)
                {
                    if (_disposed) return;
                    source.Pop();
                    if (applied) destination.Push(entry);
                }
                if (applied) return;
            }
        }
        finally { EndOperation(); }
    }

    private static IEditorHistoryEntry? PeekCurrentEntry(Stack<IEditorHistoryEntry> history)
    {
        while (history.TryPeek(out var entry))
        {
            if (entry is not NativeEditorHistoryEntry { IsStale: true }) return entry;
            history.Pop();
        }
        return null;
    }

    private void AddHistoryEntry(IEditorHistoryEntry entry)
    {
        _undo.Push(entry);
        _redo.Clear();
    }

    private void BeginOperation()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_isBusy) throw new InvalidOperationException("An Editor command operation is already running.");
            _isBusy = true;
        }
    }

    private void EndOperation()
    {
        lock (_gate) _isBusy = false;
    }
}
