using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReiEditor.Models.Services.Mcp.Editor;

public interface IEditorThreadDispatcher
{
    Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken);
    Task<T> InvokeTaskAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
}
