using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Commands;

public interface IEditorHistoryEntry
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    Task UndoAsync();
    Task RedoAsync();
}
