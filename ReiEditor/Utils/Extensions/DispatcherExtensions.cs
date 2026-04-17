using System;
using Avalonia.Threading;

namespace ReiEditor.Utils.Extensions;

public static class DispatcherExtensions
{
    public static void Execute(this Dispatcher dispatcher, Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Post(action);
    }
}
