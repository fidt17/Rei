using System.Collections.ObjectModel;
using System.Linq;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.WindowTabs;

public class WindowContainerViewModel : BaseViewModel
{
    public ObservableCollection<WindowTabViewModel> Tabs { get; } = new();

    private WindowTabViewModel? _activeTab;
    public WindowTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (!SetField(ref _activeTab, value)) return;
            UpdateActiveStates();
        }
    }

    public void AddTab(string name, BaseViewModel content, BaseViewModel? headerContent = null)
    {
        var tab = new WindowTabViewModel(name, content, headerContent, () => SetActiveTabByContent(content));
        Tabs.Add(tab);
        if (ActiveTab == null)
        {
            ActiveTab = tab;
        }
    }

    private void SetActiveTabByContent(BaseViewModel content)
    {
        var tab = Tabs.FirstOrDefault(t => t.Content == content);
        if (tab != null)
        {
            ActiveTab = tab;
        }
    }

    private void UpdateActiveStates()
    {
        foreach (var tab in Tabs)
        {
            tab.IsActive = tab == ActiveTab;
        }
    }
}
