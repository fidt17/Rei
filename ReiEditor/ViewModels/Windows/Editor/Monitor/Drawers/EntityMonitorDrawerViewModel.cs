using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReiEditor.Models.Services.Assets.Scripting;
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
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;

#pragma warning disable CS8618
    public EntityMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityMonitorDrawerViewModel(
        GameEntity entity, 
        IFactory<EntityInfoComponentDrawerViewModel> entityInfoComponentDrawerFactory,
        IFactory<BehaviourComponentDrawerViewModel> behaviourComponentDrawerFactory,
        IBehaviourRegistry behaviourRegistry,
        IEntityManagementService entityManagementService)
    {
        _entity = entity;
        _behaviourComponentDrawerFactory = behaviourComponentDrawerFactory;
        _entityManagementService = entityManagementService;
        _behaviourRegistry = behaviourRegistry;

        _entity.BehaviourAddedEvent += HandleEntityBehaviourAddedEvent;
        _entity.BehaviourDeletedEvent += HandleEntityBehaviourDeletedEvent;
        
        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
        foreach (var b in _entity.Behaviours)
        {
            Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(_entity, b));
        }

        UpdateBehaviourSelectionList();
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Elements.ClearAndDispose();
        
        _entity.BehaviourAddedEvent -= HandleEntityBehaviourAddedEvent;
        _entity.BehaviourDeletedEvent -= HandleEntityBehaviourDeletedEvent;
    }

    public void AddBehaviour(BehaviourSelectionData data)
    {
        _entityManagementService.AddBehaviour(_entity, data.BehaviourId);
    }

    private void HandleEntityBehaviourAddedEvent(GameEntity e, BehaviourComponent component)
    {
        Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(e, component));
        
        UpdateBehaviourSelectionList();
    }

    private void HandleEntityBehaviourDeletedEvent(GameEntity e, BehaviourComponent component)
    {
        foreach (var vm in Elements)
        {
            if (vm is not BehaviourComponentDrawerViewModel bvm) continue;
            if (bvm.BehaviourComponent != component) continue;
            bvm.Dispose();
            Elements.Remove(bvm);
            break;
        }
        
        UpdateBehaviourSelectionList();
    }

    private void UpdateBehaviourSelectionList()
    {
        BehaviourSelection.ClearAndDispose();
        
        var behaviourSelectionList = new List<BehaviourSelectionData>();
        foreach (var b in _behaviourRegistry.Behaviours)
        {
            if (_entity.Behaviours.FirstOrDefault(x => x.Id == b.Value.BehaviourId) != null) continue;
            behaviourSelectionList.Add(new BehaviourSelectionData(b.Key, b.Value.ObjectName));
        }
        behaviourSelectionList.Sort((a, b) => string.Compare(a.BehaviourName, b.BehaviourName, StringComparison.Ordinal));

        BehaviourSelection.AddRange(behaviourSelectionList);
    }
}