using System.Collections.ObjectModel;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class TextComponentDrawerViewModel : BaseViewModel
{
    public ObservableCollection<string> Texts { get; } = new();

    public void AddText(string info)
    {
        if (string.IsNullOrEmpty(info)) return;
        
        Texts.Add(info);
    }

    public void Reset()
    {
        Texts.Clear();
    }
}