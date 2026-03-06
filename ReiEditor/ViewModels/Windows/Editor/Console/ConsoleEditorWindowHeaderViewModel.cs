using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleEditorWindowHeaderViewModel : BaseViewModel
{
    public ConsoleFilterViewModel ConsoleFilter { get; }
    public ClearEditorConsoleCommand ClearEditorConsoleCommand { get; }

#pragma warning disable CS8618
    public ConsoleEditorWindowHeaderViewModel() { }
#pragma warning restore CS8618

    public ConsoleEditorWindowHeaderViewModel(ConsoleEditorWindowViewModel console)
    {
        ConsoleFilter = console.ConsoleFilter;
        ClearEditorConsoleCommand = console.ClearEditorConsoleCommand;
    }
}
