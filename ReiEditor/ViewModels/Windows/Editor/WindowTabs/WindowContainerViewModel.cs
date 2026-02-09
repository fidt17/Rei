using System.Collections.ObjectModel;
using System.Linq;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.WindowTabs;

public class WindowContainerViewModel : BaseViewModel
{
    public ObservableCollection<WindowTabViewModel> Tabs { get; } = new();

    private readonly IEditorPreferencesService? _preferencesService;
    private readonly string _containerTag;
    private readonly string _preferredTabName;

    private WindowTabViewModel? _activeTab;
    public WindowTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (!SetField(ref _activeTab, value)) return;
            UpdateActiveStates();
            SaveTabSelectionToPreferences();
        }
    }

    public WindowContainerViewModel()
    {
        _containerTag = "";
        _preferredTabName = "";
    }

    public WindowContainerViewModel(IEditorPreferencesService preferencesService, string containerTag)
    {
        _preferencesService = preferencesService;
        _containerTag = containerTag;
        _preferredTabName = _preferencesService.GetWindowContainerActiveTab(_containerTag);
    }

    public void AddTab(string name, BaseViewModel content, BaseViewModel? headerContent = null)
    {
        var tab = new WindowTabViewModel(name, content, headerContent, () => SetActiveTabByContent(content));
        Tabs.Add(tab);
        
        if (ActiveTab == null)
        {
            ActiveTab = tab;
        }
        
        if (string.Equals(_preferredTabName, name, System.StringComparison.Ordinal))
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

    private void SaveTabSelectionToPreferences()
    {
        if (_preferencesService == null) return;
        if (string.IsNullOrWhiteSpace(_containerTag)) return;
        if (ActiveTab == null) return;
        
        _preferencesService.SetWindowContainerActiveTab(_containerTag, ActiveTab.Name);
    }
}
