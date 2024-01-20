using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class EntityMonitorDrawerViewModel : BaseMonitorDrawer
{
    public ICommand AddBehaviourCommand { get; }
    
    public ObservableCollection<BaseViewModel> Elements { get; } = new();

    private readonly GameEntity _entity;
    private readonly IFactory<BehaviourComponentDrawerViewModel> _behaviourComponentDrawerFactory;

#pragma warning disable CS8618
    public EntityMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityMonitorDrawerViewModel(
        GameEntity entity, 
        IFactory<EntityInfoComponentDrawerViewModel> entityInfoComponentDrawerFactory,
        IFactory<BehaviourComponentDrawerViewModel> behaviourComponentDrawerFactory)
    {
        _entity = entity;
        _behaviourComponentDrawerFactory = behaviourComponentDrawerFactory;
        
        _entity.BehaviourAddedEvent += HandleEntityBehaviourAddedEvent;
        
        AddBehaviourCommand = ReactiveCommand.Create(AddBehaviour);
        
        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
        foreach (var b in _entity.Behaviours)
        {
            Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(entityInfoComponentDrawerFactory, b));
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Elements.ClearAndDispose();
        _entity.BehaviourAddedEvent -= HandleEntityBehaviourAddedEvent;
    }

    private void HandleEntityBehaviourAddedEvent(GameEntity e, BehaviourComponent component)
    {
        Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(e, component));
    }

    private void AddBehaviour()
    {
        _entity.AddBehaviour(new BehaviourComponent(0));
    }
}