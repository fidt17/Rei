using System;
using ReiEditor.Utils;

namespace ReiEditor.ViewModels.Controls;

public class ContextMenuOption
{
    public RelayCommand Command { get; }
    public string Text { get; }
    public ContextMenuViewModel? NestedMenu { get; }
    public bool HasNestedMenu => NestedMenu != null;
    public bool ShouldCloseOnExecute { get; }

    public ContextMenuOption(string text, Action? callback = null)
    {
        Text = text;

        Command = new RelayCommand();
        if (callback != null)
        {
            Command.ExecutedEvent += callback;
            ShouldCloseOnExecute = true;
        }
    }

    public ContextMenuOption(string text, ContextMenuViewModel nestedMenu)
    {
        Text = text;
        NestedMenu = nestedMenu;
        Command = new RelayCommand();
        ShouldCloseOnExecute = false;
    }
}
