using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Directories;

public class ProjectDirectoryNodeViewModel : BaseViewModel
{
    public ICommand SelectCommand { get; }

    public ObservableField<string> Name { get; }
    public ObservableField<bool> Expanded { get; } = new(false);
    public ObservableField<bool> Selected { get; } = new(false);

    public ObservableCollection<ProjectDirectoryNodeViewModel> ChildNodes { get; } = new();

    public string FullPath { get; }
    public ProjectDirectoryNodeViewModel? Parent { get; }

#pragma warning disable CS8618
    public ProjectDirectoryNodeViewModel() { }
#pragma warning restore CS8618
    
    public ProjectDirectoryNodeViewModel(string name, string fullPath, ProjectDirectoryNodeViewModel? parent)
    {
        Name = new ObservableField<string>(name);
        FullPath = fullPath;
        Parent = parent;
        SelectCommand = ReactiveCommand.Create(Select);
    }

    public void Select() => Selected.Value = true;
    public void Deselect() => Selected.Value = false;

    public override void Dispose()
    {
        base.Dispose();
        foreach (var child in ChildNodes)
        {
            child.Dispose();
        }
        ChildNodes.Clear();
    }
}
