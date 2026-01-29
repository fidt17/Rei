using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Console.Commands;

namespace ReiEditor.ViewModels.Windows.Editor.Console;

public class ConsoleEditorWindowHeaderViewModel : BaseViewModel
{
    public ConsoleFilterViewModel ConsoleFilter { get; }
    public ClearEditorConsoleCommand ClearEditorConsoleCommand { get; }

    public ConsoleEditorWindowHeaderViewModel(ConsoleEditorWindowViewModel console)
    {
        ConsoleFilter = console.ConsoleFilter;
        ClearEditorConsoleCommand = console.ClearEditorConsoleCommand;
    }
}
