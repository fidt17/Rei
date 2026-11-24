using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Commands;

public interface IEditorHistoryEntry
{
    bool IsStale => false;
    bool CanUndo { get; }
    bool CanRedo { get; }

    Task UndoAsync();
    Task RedoAsync();
}
