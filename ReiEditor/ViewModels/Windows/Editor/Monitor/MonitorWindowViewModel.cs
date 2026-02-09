using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
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

    private readonly IEntityStateSynchronizer _entityStateSynchronizer;

    private CancellationTokenSource? _entityUpdateStateCTS;

#pragma warning disable CS8618
    public MonitorWindowViewModel() { }
#pragma warning restore CS8618

    public MonitorWindowViewModel(
        ISelectionService selectionService,
        IEditorRefreshService editorRefreshService,
        IFactory<EntityMonitorDrawerViewModel> entityMonitorFactory,
        IEntityStateSynchronizer entityStateSynchronizer)
    {
        _selectionService = selectionService;
        _editorRefreshService = editorRefreshService;
        _entityMonitorFactory = entityMonitorFactory;
        _entityStateSynchronizer = entityStateSynchronizer;

        _selectionService.ActiveSelection.Subscribe(HandleActiveSelectionChangedEvent);
        _editorRefreshService.RefreshedEvent += HandleRefreshedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _selectionService.ActiveSelection.Unsubscribe(HandleActiveSelectionChangedEvent);
        _editorRefreshService.RefreshedEvent -= HandleRefreshedEvent;
        
        _entityUpdateStateCTS?.Cancel();
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
            var e = hNode.Node.Content;
            RunEntityUpdateStateTask(e);
            
            var entityMonitor = _entityMonitorFactory.CreateInstance(e);
            Drawer = entityMonitor;
        }
    }

    private void RunEntityUpdateStateTask(GameEntity e)
    {
        _entityUpdateStateCTS?.Cancel();
        _entityUpdateStateCTS = new CancellationTokenSource();

        _entityStateSynchronizer.UpdateEntityState(e);

        var token = _entityUpdateStateCTS.Token;
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(32, token);

                Dispatcher.UIThread.Invoke(() =>
                {
                    _entityStateSynchronizer.UpdateEntityState(e);
                });
            }
            // ReSharper disable once FunctionNeverReturns
        }, token);
    }
}