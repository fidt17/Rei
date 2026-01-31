using System;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Path;

public class ProjectPathSegmentViewModel : BaseViewModel
{
    public string Name { get; }
    public string FullPath { get; }
    public string SeparatorText { get; }
    public ICommand NavigateCommand { get; }

    public ProjectPathSegmentViewModel(string name, string fullPath, string separatorText, Action<ProjectPathSegmentViewModel> navigateAction)
    {
        Name = name;
        FullPath = fullPath;
        SeparatorText = separatorText;
        NavigateCommand = ReactiveCommand.Create(() => navigateAction(this));
    }
}
