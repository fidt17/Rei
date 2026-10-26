using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Commands;

public interface IEditorCommand<in TArgs, TResult>
{
    Task<TResult> ExecuteAsync(TArgs args);
}
