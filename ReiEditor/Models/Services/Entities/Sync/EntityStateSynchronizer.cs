using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntityStateSynchronizer : IEntityStateSynchronizer, IDisposable
{
    private bool _ignorePropertyChanges;
    private CancellationTokenSource? _sceneUpdateTaskCTS;
    
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ISelectionService _selectionService;
    private readonly ILogger<EntityStateSynchronizer> _logger;
    private readonly ISceneManagementService _sceneManagement;
    private readonly EntityStateApplier _stateApplier;
    
    public EntityStateSynchronizer(
        IBehaviourRegistry behaviourRegistry,
        IAssetImporter assetImporter,
        IEntityApi entityApi,
        ISelectionService selectionService,
        ILogger<EntityStateSynchronizer> logger,
        ISceneManagementService sceneManagement, 
        IEngineRunner engineRunner, 
        IBehaviourComponentsService behaviourComponentsService)
    {
        _behaviourRegistry = behaviourRegistry;
        _assetImporter = assetImporter;
        _entityApi = entityApi;
        _selectionService = selectionService;
        _logger = logger;
        _sceneManagement = sceneManagement;
        _engineRunner = engineRunner;
        _behaviourComponentsService = behaviourComponentsService;
        _stateApplier = new EntityStateApplier(_logger, _behaviourRegistry, _behaviourComponentsService);

        _behaviourComponentsService.BehaviourPropertyChangedEvent += HandleEntityBehaviourPropertyChangedEvent;
        _engineRunner.IsActive.Subscribe(HandleEngineRunningValueChangedEvent, invoke: false);
    }
    
    public void Dispose()
    {
        _sceneUpdateTaskCTS?.Cancel();
        _sceneUpdateTaskCTS?.Dispose();
        _sceneUpdateTaskCTS = null;

        _behaviourComponentsService.BehaviourPropertyChangedEvent -= HandleEntityBehaviourPropertyChangedEvent;
        _engineRunner.IsActive.Unsubscribe(HandleEngineRunningValueChangedEvent);
    }

    public void UpdateEntityState(GameEntity e)
    {
        if (!_engineRunner.IsActive.Value) return;
        if (_assetImporter.IsImporting.Value) return;
        
        var state = _entityApi.GetEntityData(e.Id);
        var needsHierarchyRefresh = UpdateEntityStateFromEngineState(e, state);
        
        if (needsHierarchyRefresh)
        {
            _sceneManagement.CurrentScene.Value!.RebuildHierarchy();
        }
    }
    
    private void HandleEntityBehaviourPropertyChangedEvent(EntityBehaviourPropertyChangeEventArgs args)
    {
        if (!_engineRunner.IsActive.Value) return;
        if (_assetImporter.IsImporting.Value) return;
        
        if (_ignorePropertyChanges) return;
        
        try
        {
            var request = new SetEntityDataRequest
            {
                SceneId = args.Entity.Id,
                Behaviours = new List<Dictionary<string, object?>>()
            };
            
            var behaviourData = new Dictionary<string, object?> { { "REI_BEHAVIOUR_ID", args.Component.Id } };
            request.Behaviours.Add(behaviourData);

            var propertyToSync = GetPropertyRootForSync(args.Property);
            behaviourData.Add(propertyToSync.Name, SerializePropertyChange(propertyToSync));
            
            if (request.Behaviours.Count == 0) return;

            _entityApi.SetData(request);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
    }

    private static SerializedProperty GetPropertyRootForSync(SerializedProperty property)
    {
        var current = property;
        while (current.ParentProperty is { } parent && (parent.Type == SerializedTypeEnum.Custom || parent.Type == SerializedTypeEnum.Collection))
        {
            current = parent;
        }

        return current;
    }

    private static Dictionary<string, object?> SerializePropertyChange(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            var serializedItems = new List<object?>();
            if (property.Value is List<SerializedProperty> collectionItems)
            {
                foreach (var item in collectionItems)
                {
                    serializedItems.Add(SerializePropertyValue(item));
                }
            }

            return new Dictionary<string, object?>
            {
                { "Value", serializedItems }
            };
        }

        if (property.Type != SerializedTypeEnum.Custom)
        {
            return new Dictionary<string, object?>
            {
                { "Value", property.Value }
            };
        }

        var serializedChildren = new Dictionary<string, object?>();
        if (property.Value is Dictionary<string, SerializedProperty> nestedProperties)
        {
            foreach (var nestedProperty in nestedProperties.Values)
            {
                serializedChildren[nestedProperty.Name] = SerializePropertyChange(nestedProperty);
            }
        }

        return new Dictionary<string, object?>
        {
            { "Value", serializedChildren }
        };
    }

    private static object? SerializePropertyValue(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            var serializedItems = new List<object?>();
            if (property.Value is List<SerializedProperty> collectionItems)
            {
                foreach (var item in collectionItems)
                {
                    serializedItems.Add(SerializePropertyValue(item));
                }
            }

            return serializedItems;
        }

        if (property.Type != SerializedTypeEnum.Custom) return property.Value;

        var serializedChildren = new Dictionary<string, object?>();
        if (property.Value is Dictionary<string, SerializedProperty> nestedProperties)
        {
            foreach (var nestedProperty in nestedProperties.Values)
            {
                serializedChildren[nestedProperty.Name] = SerializePropertyChange(nestedProperty);
            }
        }

        return serializedChildren;
    }
    
    private void HandleEngineRunningValueChangedEvent(bool isRunning)
    {
        if (isRunning)
        {
            _sceneUpdateTaskCTS?.Cancel();
            _sceneUpdateTaskCTS = new CancellationTokenSource();

            var token = _sceneUpdateTaskCTS.Token;
            Task.Run(async () =>
            {
                while (_engineRunner.IsActive.Value && !token.IsCancellationRequested)
                {
                    await Task.Delay(33, token);

                    UpdateSceneEntitiesFromEngine();
                }
            }, token);
        }
        else
        {
            _sceneUpdateTaskCTS?.Cancel();
        }
    }
    
    private void UpdateSceneEntitiesFromEngine()
    {
        try
        {
            if (!_engineRunner.IsActive.Value) return;
            if (_assetImporter.IsImporting.Value) return;

            var selectionBeforeSync = CaptureSelectedEntityIds();

            var entities = _entityApi.GetSceneEntities();
            if (entities == null) return;

            var scene = _sceneManagement.CurrentScene.Value;
            if (scene == null) throw new Exception("Current scene is missing");

            var currentSceneEntities = scene.Entities.ToList();
            //_logger.LogWarning($"Scene entities: {JsonConvert.SerializeObject(entities)}");
            var needsHierarchyRefresh = false;
            var selectedEntityIds = new HashSet<int>();

            var entityStates = new Dictionary<int, GetEntityDataResponse?>();
            var parentByEntityId = new Dictionary<int, int>();
            var orderByEntityId = new Dictionary<int, int>();

            foreach (var entity in entities.Entities)
            {
                var state = _entityApi.GetEntityData(entity.Id);
                entityStates[entity.Id] = state;

                var parentId = 0;
                var order = 0;
                if (state != null && EntityStateSyncUtility.TryGetTransformData(state.Behaviours, out var transformParent, out var transformOrder))
                {
                    parentId = transformParent ?? 0;
                    order = transformOrder ?? 0;
                }

                parentByEntityId[entity.Id] = parentId;
                orderByEntityId[entity.Id] = order;
            }

            var orderedEntityIds = EntityStateSyncUtility.BuildOrderedEntityIds(parentByEntityId, orderByEntityId);

            foreach (var entityId in orderedEntityIds)
            {
                var e = entities.Entities.Find(x => x.Id == entityId);
                if (e == null) continue;

                var gameEntity = currentSceneEntities.Find(x => x.Id == e.Id);
                // Create missing entities
                if (gameEntity == null)
                {
                    gameEntity = new GameEntity(e.Id, $"Entity {e}");
                    scene.AddEntity(gameEntity);
                    needsHierarchyRefresh |= UpdateEntityStateFromEngineState(gameEntity, entityStates[entityId]);
                }
                else
                {
                    needsHierarchyRefresh |= UpdateEntityStateFromEngineState(gameEntity, entityStates[entityId]);
                }

                if (e.IsSelected)
                {
                    selectedEntityIds.Add(gameEntity.Id);
                }
            }
            
            // Delete invalid entities
            foreach (var gameEntity in currentSceneEntities.Where(x => !entities.Entities.Exists(y => y.Id == x.Id)))
            {
                scene.DeleteEntity(gameEntity, refreshTransforms: false);
                needsHierarchyRefresh = true;
            }

            if (needsHierarchyRefresh)
            {
                scene.RebuildHierarchy();
            }

            if (selectionBeforeSync.SetEquals(CaptureSelectedEntityIds()))
            {
                UpdateEntitySelection(scene.Entities.ToList(), selectedEntityIds);
            }
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }

    private bool UpdateEntityStateFromEngineState(GameEntity e, GetEntityDataResponse? state)
    {
        if (state == null) return false;

        _ignorePropertyChanges = true;
        try
        {
            return _stateApplier.Apply(e, state);
        }
        finally
        {
            _ignorePropertyChanges = false;
        }
    }

    private void UpdateEntitySelection(IReadOnlyCollection<GameEntity> entities, IReadOnlySet<int> selectedEntityIds)
    {
        var selectedItems = entities
            .Where(entity => selectedEntityIds.Contains(entity.Id))
            .Select(entity => _selectionService.GetEntitySelectable(entity))
            .Where(selectable => selectable != null)
            .Cast<ISelectable>()
            .ToList();

        if (selectedItems.Count == 0)
        {
            if (_selectionService.SelectedItems.OfType<IEntitySelectable>().Any())
            {
                _selectionService.ResetSelection(sendToEngine: false);
            }

            return;
        }

        var currentPrimarySelection = _selectionService.ActiveSelection.Value as IEntitySelectable;
        var primarySelection = currentPrimarySelection != null &&
                               selectedEntityIds.Contains(currentPrimarySelection.Entity.Id)
            ? currentPrimarySelection
            : selectedItems.OfType<IEntitySelectable>().FirstOrDefault();
        if (primarySelection == null)
        {
            _selectionService.ResetSelection(sendToEngine: false);
            return;
        }

        _selectionService.SetSelection(selectedItems, primarySelection, sendToEngine: false);
    }

    private HashSet<int> CaptureSelectedEntityIds()
    {
        return _selectionService.SelectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)
            .ToHashSet();
    }
}
