using System;
using System.Collections.ObjectModel;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Controls;

public class ContextMenuViewModel : BaseViewModel
{
    public event Action? AnyCommandExecutedEvent;

    public ObservableCollection<ContextMenuOption> Options { get; } = new();

    public void AddOption(ContextMenuOption option)
    {
        Options.Add(option);
        if (option.IsSeparator) return;

        if (option.ShouldCloseOnExecute)
        {
            option.Command.ExecutedEvent += () => AnyCommandExecutedEvent?.Invoke();
        }

        if (option.NestedMenu != null)
        {
            option.NestedMenu.AnyCommandExecutedEvent += () => AnyCommandExecutedEvent?.Invoke();
        }
    }

    public ContextMenuViewModel Clone()
    {
        var clone = new ContextMenuViewModel();
        foreach (var option in Options)
        {
            clone.AddOption(option.Clone());
        }

        return clone;
    }
}
