using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData;
using ReiEditor.Models.Services.Assets.Behaviours;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class BehaviourSelectionData
{
    public int BehaviourId { get; }
    public string BehaviourName { get; }

    public BehaviourSelectionData(int behaviourId, string behaviourName)
    {
        BehaviourId = behaviourId;
        BehaviourName = behaviourName;
    }
}

public class EntityMonitorDrawerViewModel : BaseMonitorDrawer
{
    public ObservableCollection<BaseViewModel> Elements { get; } = new();
    public ObservableCollection<BehaviourSelectionData> BehaviourSelection { get; } = new();

    private readonly GameEntity _entity;
    private readonly IFactory<BehaviourComponentDrawerViewModel> _behaviourComponentDrawerFactory;

#pragma warning disable CS8618
    public EntityMonitorDrawerViewModel()
    {
    }
#pragma warning restore CS8618

    public EntityMonitorDrawerViewModel(
        GameEntity entity, 
        IFactory<EntityInfoComponentDrawerViewModel> entityInfoComponentDrawerFactory,
        IFactory<BehaviourComponentDrawerViewModel> behaviourComponentDrawerFactory,
        IBehaviourComponentsService behaviourComponentsService)
    {
        _entity = entity;
        _behaviourComponentDrawerFactory = behaviourComponentDrawerFactory;

        _entity.BehaviourAddedEvent += HandleEntityBehaviourAddedEvent;
        
        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
        foreach (var b in _entity.Behaviours)
        {
            Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(_entity, b));
        }

        ConfigureBehaviourSelectionList(behaviourComponentsService);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Elements.ClearAndDispose();
        _entity.BehaviourAddedEvent -= HandleEntityBehaviourAddedEvent;
    }

    public void AddBehaviour(int idx)
    {
        var bd = BehaviourSelection[idx];
        _entity.AddBehaviour(new BehaviourComponent(bd.BehaviourId));
    }

    private void HandleEntityBehaviourAddedEvent(GameEntity e, BehaviourComponent component)
    {
        Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(e, component));
    }

    private void ConfigureBehaviourSelectionList(IBehaviourComponentsService behaviourComponentsService)
    {
        var behaviourSelectionList = new List<BehaviourSelectionData>();
        foreach (var b in behaviourComponentsService.Behaviours)
        {
            behaviourSelectionList.Add(new BehaviourSelectionData(b.Key, b.Value.BehaviourName));
        }

        BehaviourSelection.AddRange(behaviourSelectionList);
    }
}