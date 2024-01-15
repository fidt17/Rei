using System.Collections.ObjectModel;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class EntityMonitorDrawerViewModel : BaseMonitorDrawer
{
    public ObservableCollection<BaseViewModel> Elements { get; } = new();

    private readonly GameEntity _entity;
    
#pragma warning disable CS8618
    public EntityMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityMonitorDrawerViewModel(
        GameEntity entity, 
        IFactory<EntityInfoComponentDrawerViewModel> entityInfoComponentDrawerFactory)
    {
        _entity = entity;

        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
    }
}