using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DynamicData;
using ReiEditor.Models.Services.Assets.Behaviours;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
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
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly ILogger<EntityMonitorDrawerViewModel> _logger;

#pragma warning disable CS8618
    public EntityMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityMonitorDrawerViewModel(
        GameEntity entity, 
        IFactory<EntityInfoComponentDrawerViewModel> entityInfoComponentDrawerFactory,
        IFactory<BehaviourComponentDrawerViewModel> behaviourComponentDrawerFactory,
        IBehaviourComponentsService behaviourComponentsService,
        IBehaviourRegistry behaviourRegistry,
        ILogger<EntityMonitorDrawerViewModel> logger)
    {
        _entity = entity;
        _behaviourComponentDrawerFactory = behaviourComponentDrawerFactory;
        _behaviourComponentsService = behaviourComponentsService;
        _logger = logger;

        _entity.BehaviourAddedEvent += HandleEntityBehaviourAddedEvent;
        _entity.BehaviourDeletedEvent += HandleEntityBehaviourDeletedEvent;
        
        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
        foreach (var b in _entity.Behaviours)
        {
            Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(_entity, b));
        }

        ConfigureBehaviourSelectionList(behaviourRegistry);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Elements.ClearAndDispose();
        _entity.BehaviourAddedEvent -= HandleEntityBehaviourAddedEvent;
    }

    public void AddBehaviour(BehaviourSelectionData data)
    {
        if (_behaviourComponentsService.AddComponent(_entity, data.BehaviourId)) return;
        
        _logger.LogError($"Failed at adding component {data.BehaviourId}:{data.BehaviourName}");
    }

    private void HandleEntityBehaviourAddedEvent(GameEntity e, BehaviourComponent component)
    {
        Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(e, component));
    }

    private void HandleEntityBehaviourDeletedEvent(GameEntity e, BehaviourComponent component)
    {
        foreach (var vm in Elements)
        {
            if (vm is not BehaviourComponentDrawerViewModel bvm) continue;
            if (bvm.BehaviourComponent != component) continue;
            bvm.Dispose();
            Elements.Remove(bvm);
            return;
        }
    }

    private void ConfigureBehaviourSelectionList(IBehaviourRegistry behaviourRegistry)
    {
        var behaviourSelectionList = new List<BehaviourSelectionData>();
        foreach (var b in behaviourRegistry.Behaviours)
        {
            behaviourSelectionList.Add(new BehaviourSelectionData(b.Key, b.Value.BehaviourName));
        }
        behaviourSelectionList.Sort((a, b) => string.Compare(a.BehaviourName, b.BehaviourName, StringComparison.Ordinal));

        BehaviourSelection.AddRange(behaviourSelectionList);
    }
}