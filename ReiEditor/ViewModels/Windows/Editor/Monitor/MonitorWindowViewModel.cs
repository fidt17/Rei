using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor;

public class MonitorWindowViewModel : BaseViewModel
{
    #region Drawer

    private BaseMonitorDrawer? _drawer;
    public BaseMonitorDrawer? Drawer
    {
        get => _drawer;
        private set => SetField(ref _drawer, value);
    }

    #endregion
    
    private readonly ISelectionService _selectionService;
    private readonly IEditorRefreshService _editorRefreshService;
    private readonly IFactory<EntityMonitorDrawerViewModel> _entityMonitorFactory;

#pragma warning disable CS8618
    public MonitorWindowViewModel() { }
#pragma warning restore CS8618

    public MonitorWindowViewModel(
        ISelectionService selectionService,
        IEditorRefreshService editorRefreshService,
        IFactory<EntityMonitorDrawerViewModel> entityMonitorFactory)
    {
        _selectionService = selectionService;
        _editorRefreshService = editorRefreshService;
        _entityMonitorFactory = entityMonitorFactory;

        _selectionService.ActiveSelection.Subscribe(HandleActiveSelectionChangedEvent);
        _editorRefreshService.RefreshedEvent += HandleRefreshedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        _selectionService.ActiveSelection.Unsubscribe(HandleActiveSelectionChangedEvent);
        _editorRefreshService.RefreshedEvent -= HandleRefreshedEvent;
    }

    private void HandleActiveSelectionChangedEvent(ISelectable? obj)
    {
        UpdateDrawer(obj);
    }

    private void HandleRefreshedEvent()
    {
        UpdateDrawer(_selectionService.ActiveSelection.Value);
    }

    private void UpdateDrawer(ISelectable? obj)
    {
        if (Drawer != null)
        {
            Drawer.Dispose();
            Drawer = null;
        }
        
        if (obj is HierarchyNodeViewModel hNode)
        {
            GameEntity e = hNode.Node.Content;
            Drawer = _entityMonitorFactory.CreateInstance(e);
        }
    }
}