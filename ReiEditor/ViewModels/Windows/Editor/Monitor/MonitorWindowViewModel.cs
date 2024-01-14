using ReiEditor.Models.EditorApp.Selection;
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
    private readonly IFactory<EntityMonitorDrawerViewModel> _entityMonitorFactory;

#pragma warning disable CS8618
    public MonitorWindowViewModel() { }
#pragma warning restore CS8618

    public MonitorWindowViewModel(
        ISelectionService selectionService, 
        IFactory<EntityMonitorDrawerViewModel> entityMonitorFactory)
    {
        _selectionService = selectionService;
        _entityMonitorFactory = entityMonitorFactory;

        _selectionService.SelectionChangedEvent += HandleSelectionChangedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        _selectionService.SelectionChangedEvent -= HandleSelectionChangedEvent;
    }

    private void HandleSelectionChangedEvent(ISelectable? obj)
    {
        if (obj is HierarchyNodeViewModel hNode)
        {
            var e = hNode.Node.Content;
            Drawer = _entityMonitorFactory.CreateInstance(e);
        }
    }
}