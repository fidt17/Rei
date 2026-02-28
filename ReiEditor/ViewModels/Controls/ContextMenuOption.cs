using System;
using ReiEditor.Utils;

namespace ReiEditor.ViewModels.Controls;

public class ContextMenuOption
{
    private readonly Action? _callback;

    public RelayCommand Command { get; }
    public string Text { get; }
    public ContextMenuViewModel? NestedMenu { get; }
    public bool HasNestedMenu => NestedMenu != null;
    public bool ShouldCloseOnExecute { get; }
    public bool IsSeparator { get; }

    public ContextMenuOption(string text, Action? callback = null)
    {
        Text = text;
        _callback = callback;

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

    private ContextMenuOption(bool isSeparator)
    {
        IsSeparator = isSeparator;
        Text = "";
        Command = new RelayCommand();
        ShouldCloseOnExecute = false;
    }

    public static ContextMenuOption Separator()
    {
        return new ContextMenuOption(isSeparator: true);
    }

    public ContextMenuOption Clone()
    {
        if (IsSeparator) return Separator();
        if (NestedMenu != null) return new ContextMenuOption(Text, NestedMenu.Clone());
        return new ContextMenuOption(Text, _callback);
    }
}
