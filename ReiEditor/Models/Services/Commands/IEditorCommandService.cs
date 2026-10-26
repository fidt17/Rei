using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;

namespace ReiEditor.Models.Services.Commands;

public interface IEditorCommandService
{
    bool IsBusy { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }

    Task<TResult> ExecuteAsync<TArgs, TResult>(IEditorCommand<TArgs, TResult> command, TArgs args);
    Task UndoAsync();
    Task RedoAsync();
}
