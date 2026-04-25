using System;
using ReiEditor.Utils;

namespace ReiEditor.ViewModels.Controls;

public class ContextMenuOption
{
    private readonly Action? _callback;
    private readonly Func<bool>? _canExecuteFunction;
    private readonly Func<string?>? _toolTipFunction;

    public RelayCommand Command { get; }
    public string Text { get; }
    public ContextMenuViewModel? NestedMenu { get; }
    public bool HasNestedMenu => NestedMenu != null;
    public bool ShouldCloseOnExecute { get; }
    public bool IsSeparator { get; }
    public string? ToolTip => _toolTipFunction?.Invoke();
    public bool IsEnabled => _canExecuteFunction?.Invoke() ?? true;

    public ContextMenuOption(string text, Action? callback = null, Func<bool>? canExecuteFunction = null, Func<string?>? toolTipFunction = null)
    {
        Text = text;
        _callback = callback;
        _canExecuteFunction = canExecuteFunction;
        _toolTipFunction = toolTipFunction;

        Command = new RelayCommand(canExecuteFunction: () => IsEnabled);
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
        return new ContextMenuOption(Text, _callback, _canExecuteFunction, _toolTipFunction);
    }
}
