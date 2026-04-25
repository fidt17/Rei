using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class BehaviourSelectionData
{
    public int BehaviourId { get; }
    public string BehaviourName { get; }
    public RelayCommand SelectCommand { get; }

    public BehaviourSelectionData(int behaviourId, string behaviourName, Action selectAction)
    {
        BehaviourId = behaviourId;
        BehaviourName = behaviourName;
        SelectCommand = new RelayCommand(selectAction);
    }
}

public class EntityMonitorDrawerViewModel : BaseMonitorDrawer
{
    public event Action? BehaviourSelectedEvent;

    public ObservableCollection<BaseViewModel> Elements { get; } = new();
    public SearchFieldViewModel SearchField { get; } = new();
    public ObservableCollection<BehaviourSelectionData> BehaviourSelection { get; } = new();

    private readonly GameEntity _entity;
    private readonly IFactory<BehaviourComponentDrawerViewModel> _behaviourComponentDrawerFactory;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly List<BehaviourSelectionData> _allBehaviourSelection = new();

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
        SearchField.Query.ChangedEvent += HandleSearchQueryChanged;
        
        Elements.Add(entityInfoComponentDrawerFactory.CreateInstance(entity));
        foreach (var b in _entity.Behaviours)
        {
            if (ShouldHideBehaviour(b)) continue;
            Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(_entity, b));
        }

        UpdateBehaviourSelectionList();
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Elements.ClearAndDispose();
        BehaviourSelection.ClearAndDispose();
        _allBehaviourSelection.Clear();
        
        _entity.BehaviourAddedEvent -= HandleEntityBehaviourAddedEvent;
        _entity.BehaviourDeletedEvent -= HandleEntityBehaviourDeletedEvent;
        SearchField.Query.ChangedEvent -= HandleSearchQueryChanged;
        SearchField.Dispose();
    }

    public void AddBehaviour(BehaviourSelectionData data)
    {
        _entityManagementService.AddBehaviour(_entity, data.BehaviourId);
        BehaviourSelectedEvent?.Invoke();
    }

    private void HandleEntityBehaviourAddedEvent(GameEntity e, BehaviourComponent component)
    {
        if (IsRectTransform(component))
        {
            RemoveBehaviourDrawer(EngineBehavioursConstants.TRANSFORM);
        }

        if (ShouldHideBehaviour(component)) return;

        Elements.Add(_behaviourComponentDrawerFactory.CreateInstance(e, component));

        UpdateBehaviourSelectionList();
    }

    private void HandleEntityBehaviourDeletedEvent(GameEntity e, BehaviourComponent component)
    {
        var target = Elements
            .OfType<BehaviourComponentDrawerViewModel>()
            .FirstOrDefault(x => x.BehaviourComponent == component);
        if (target != null)
        {
            target.Dispose();
            Elements.Remove(target);
        }

        if (IsRectTransform(component))
        {
            AddTransformDrawerIfVisible();
        }
        
        UpdateBehaviourSelectionList();
    }

    private void UpdateBehaviourSelectionList()
    {
        _allBehaviourSelection.Clear();
        
        foreach (var b in _behaviourRegistry.Behaviours)
        {
            if (_entity.Behaviours.FirstOrDefault(x => x.Id == b.Value.BehaviourId) != null) continue;
            _allBehaviourSelection.Add(new BehaviourSelectionData(b.Key, b.Value.ObjectName, () => AddBehaviourById(b.Key)));
        }

        _allBehaviourSelection.Sort((a, b) => string.Compare(a.BehaviourName, b.BehaviourName, StringComparison.Ordinal));
        RefreshBehaviourSelection(SearchField.Query.Value);
    }

    private void AddBehaviourById(int behaviourId)
    {
        var selected = _allBehaviourSelection.FirstOrDefault(x => x.BehaviourId == behaviourId);
        if (selected == null) return;

        AddBehaviour(selected);
    }

    private void HandleSearchQueryChanged(string query)
    {
        if (SearchField.ShouldSuppressQueryRefresh()) return;
        RefreshBehaviourSelection(query);
    }

    private void RefreshBehaviourSelection(string query)
    {
        BehaviourSelection.ClearAndDispose();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allBehaviourSelection
            : _allBehaviourSelection
                .Where(x => x.BehaviourName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        BehaviourSelection.AddRange(filtered);
    }

    private bool ShouldHideBehaviour(BehaviourComponent component)
    {
        if (!_behaviourRegistry.TryGetById(component.Id, out var behaviourInfo)) return false;
        if (behaviourInfo.ObjectName != EngineBehavioursConstants.TRANSFORM) return false;

        var rectTransformId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.RECT_TRANSFORM);
        return rectTransformId != null && _entity.HasComponent(rectTransformId.Value);
    }

    private bool IsRectTransform(BehaviourComponent component)
    {
        if (!_behaviourRegistry.TryGetById(component.Id, out var behaviourInfo)) return false;
        return behaviourInfo.ObjectName == EngineBehavioursConstants.RECT_TRANSFORM;
    }

    private void RemoveBehaviourDrawer(string behaviourName)
    {
        var target = Elements
            .OfType<BehaviourComponentDrawerViewModel>()
            .FirstOrDefault(x =>
                _behaviourRegistry.TryGetById(x.BehaviourComponent.Id, out var behaviourInfo) &&
                behaviourInfo.ObjectName == behaviourName);
        if (target == null) return;

        target.Dispose();
        Elements.Remove(target);
    }

    private void AddTransformDrawerIfVisible()
    {
        var transformId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        if (transformId == null) return;

        var transform = _entity.GetBehaviour(transformId.Value);
        if (transform == null || ShouldHideBehaviour(transform)) return;

        Elements.Insert(1, _behaviourComponentDrawerFactory.CreateInstance(_entity, transform));
    }
}
