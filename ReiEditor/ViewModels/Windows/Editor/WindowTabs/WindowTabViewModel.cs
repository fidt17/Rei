using System;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.WindowTabs;

public class WindowTabViewModel : BaseViewModel
{
    public string Name { get; }
    public BaseViewModel Content { get; }
    public BaseViewModel? HeaderContent { get; }
    public RelayCommand SelectCommand { get; }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public WindowTabViewModel(string name, BaseViewModel content, BaseViewModel? headerContent, Action onSelect)
    {
        Name = name;
        Content = content;
        HeaderContent = headerContent;
        SelectCommand = new RelayCommand(onSelect);
    }
}
