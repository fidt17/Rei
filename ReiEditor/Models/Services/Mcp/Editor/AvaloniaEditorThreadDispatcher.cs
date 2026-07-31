using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ReiEditor.Models.Services.Mcp.Editor;

public sealed class AvaloniaEditorThreadDispatcher : IEditorThreadDispatcher
{
    public async Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess()) return operation();

        return await Dispatcher.UIThread.InvokeAsync(operation, DispatcherPriority.Normal, cancellationToken);
    }

    public async Task<T> InvokeTaskAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess()) return await operation();

        var operationTask = await Dispatcher.UIThread.InvokeAsync(operation, DispatcherPriority.Normal, cancellationToken);
        return await operationTask.WaitAsync(cancellationToken);
    }
}
