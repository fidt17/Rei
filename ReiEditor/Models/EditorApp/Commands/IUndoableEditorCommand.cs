namespace ReiEditor.Models.EditorApp.Commands;

public interface IUndoableEditorCommand<in TArgs, TResult> : IEditorCommand<TArgs, TResult>
{
    IEditorHistoryEntry? CreateHistoryEntry(TArgs args, TResult result);
}
