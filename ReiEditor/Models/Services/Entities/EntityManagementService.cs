using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Entities;

public class EntityManagementService : IEntityManagementService
{
    private readonly ILogger<EntityManagementService> _logger;
    private readonly ISceneManagementService _sceneManagement;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEntitySyncService _entitySyncService;
    private readonly IRectTransformLayoutService _rectTransformLayoutService;

    public EntityManagementService(
        ILogger<EntityManagementService> logger,
        ISceneManagementService sceneManagement,
        IBehaviourComponentsService behaviourComponentsService,
        IEntityApi entityApi, 
        IBehaviourRegistry behaviourRegistry, 
        IEngineRunner engineRunner, 
        IEntitySyncService entitySyncService,
        IRectTransformLayoutService rectTransformLayoutService)
    {
        _logger = logger;
        _sceneManagement = sceneManagement;
        _behaviourComponentsService = behaviourComponentsService;
        _entityApi = entityApi;
        _behaviourRegistry = behaviourRegistry;
        _engineRunner = engineRunner;
        _entitySyncService = entitySyncService;
        _rectTransformLayoutService = rectTransformLayoutService;

    }

    public Task<GameEntity?> CreateEntity(string name, GameEntity? parent = null)
    {
        try
        {
            if (_engineRunner.IsActive.Value)
            {
                var scene = _sceneManagement.CurrentScene.Value;
                if (scene == null) throw new Exception("Current scene is missing");

                var existingIds = scene.Entities.Select(x => x.Id).ToHashSet();
                var response = _entityApi.CreateNewEntity(name);
                var entity = response?.EntityId == null
                    ? WaitForCreatedEntity(name, existingIds)
                    : CreateSyncedEntity(scene, response.EntityId.Value);
                if (entity != null && parent != null)
                {
                    SetParent(entity, parent, GetChildInsertionIndex(parent));
                }
                return Task.FromResult(entity);
            }

            if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");
            
            var s = _sceneManagement.CurrentScene.Value;
            s.NormalizeTransformOrders();
            var maxRootOrder = s.Entities
                .Where(x => !x.Transform.HasParent())
                .Select(x => x.Transform.Order)
                .DefaultIfEmpty(-1)
                .Max();
            
            var e = new GameEntity(s.AllocateEntityId(), name);
            var transformBehaviourId = _behaviourRegistry.GetIdByName("Transform")!.Value;
            AddBehaviour(e, transformBehaviourId);
            
            e.Transform.SetParent(0);
            e.Transform.SetOrder(maxRootOrder + 1);
            SyncTransformBehaviourProperties(e);
            
            s.AddEntity(e);
            if (parent != null)
            {
                s.MoveEntity(e, parent, int.MaxValue);
                SyncTransformBehaviourProperties(e);
            }
            return Task.FromResult<GameEntity?>(e);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }

        return Task.FromResult<GameEntity?>(null);
    }

    public void RenameEntity(GameEntity e, string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
            {
                name = $"Entity {e.Id}";
            }
            
            if (e.Name == name) return;

            if (_engineRunner.IsActive.Value)
            {
                _entityApi.Rename(e.Id, name);
                _entitySyncService.UpdateEntityState(e);
            }
            else
            {
                e.SetName(name);
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void SetParent(GameEntity e, GameEntity? parent, int idx)
    {
        if (e == parent) return;
        
        if (!_engineRunner.IsActive.Value) return;

        try
        {
            var shouldPreserveRectTransform = _rectTransformLayoutService.TryPreserveRectForParent(e, parent, out var preservedLayout);
            _entityApi.SetEntityParent(e.Id, parent?.Id ?? 0, idx);
            if (shouldPreserveRectTransform && _rectTransformLayoutService.TryGetRectTransform(e, out var rectTransform))
            {
                ApplyRectTransformLayout(e, rectTransform, preservedLayout);
            }
            _entitySyncService.UpdateEntityState(e);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void AddBehaviour(GameEntity e, int behaviourId)
    {
        try
        {
            if (_engineRunner.IsActive.Value)
            {
                _entityApi.AddBehaviour(e.Id, behaviourId);
                _entitySyncService.UpdateEntityState(e);
            }
            else
            {
                _behaviourComponentsService.AddComponent(e, behaviourId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void DeleteBehaviour(GameEntity e, int behaviourId)
    {
        try
        {
            var behaviour = e.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
            if (behaviour == null) throw new Exception($"Entity {e} does not have a behaviour with id={behaviourId}");
            
            if (_engineRunner.IsActive.Value)
            {
                _entityApi.DeleteBehaviour(e.Id, behaviourId);
                _entitySyncService.UpdateEntityState(e);
            }
            else
            {
                _behaviourComponentsService.DeleteComponent(e, behaviour);
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public int? InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true)
    {
        try
        {
            if (!_engineRunner.IsActive.Value)
            {
                throw new Exception("Instantiate entity requires engine to be running");
            }

            var baseName = string.IsNullOrWhiteSpace(requestedName) ? sourceEntity.Name : requestedName;
            var scene = _sceneManagement.CurrentScene.Value;
            var existingIds = scene?.Entities.Select(x => x.Id).ToHashSet() ?? new System.Collections.Generic.HashSet<int>();
            var uniqueName = scene == null
                ? baseName
                : NamingUtils.GetUniqueName(baseName, scene.Entities.Select(x => x.Name));

            var response = _entityApi.InstantiateEntity(new InstantiateEntityRequest
            {
                SourceEntityId = sourceEntity.Id,
                RequestedName = uniqueName,
                IncludeChildren = includeChildren
            });

            if (scene != null)
            {
                SyncInstantiatedEntities(scene, existingIds);
            }

            return response?.EntityId;
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }

        return null;
    }

    private void SyncInstantiatedEntities(Scene scene, System.Collections.Generic.HashSet<int> existingIds)
    {
        var entities = _entityApi.GetSceneEntities();
        if (entities == null) return;

        var newEntityIds = entities.Entities
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSet();
        if (newEntityIds.Count == 0) return;

        var parentByEntityId = new System.Collections.Generic.Dictionary<int, int>();
        var orderByEntityId = new System.Collections.Generic.Dictionary<int, int>();

        foreach (var entityId in newEntityIds)
        {
            var state = _entityApi.GetEntityData(entityId);
            var parentId = 0;
            var order = 0;
            if (state != null && EntitySyncUtility.TryGetTransformData(state.Behaviours, out var transformParent, out var transformOrder))
            {
                parentId = transformParent ?? 0;
                order = transformOrder ?? 0;
            }

            parentByEntityId[entityId] = parentId;
            orderByEntityId[entityId] = order;
        }

        foreach (var entityId in EntitySyncUtility.BuildOrderedEntityIds(parentByEntityId, orderByEntityId))
        {
            var gameEntity = scene.GetById(entityId);
            if (gameEntity == null)
            {
                gameEntity = new GameEntity(entityId, $"Entity {entityId}");
                scene.AddEntity(gameEntity);
            }

            _entitySyncService.UpdateEntityState(gameEntity);
        }

        scene.RebuildHierarchy();
    }

    public void DestroyEntity(GameEntity e)
    {
        try
        {
            if (_engineRunner.IsActive.Value)
            {
                _entityApi.DestroyEntity(e.Id);
                _sceneManagement.CurrentScene.Value?.DeleteEntity(e);
            }
            else
            {
                if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

                var s = _sceneManagement.CurrentScene.Value;
                s.DeleteEntity(e);
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    private GameEntity? CreateSyncedEntity(Scene scene, int entityId)
    {
        var state = _entityApi.GetEntityData(entityId);
        if (state == null) return null;

        var gameEntity = scene.GetById(entityId);
        if (gameEntity != null) return gameEntity;

        gameEntity = new GameEntity(entityId, state.Name);
        scene.AddEntity(gameEntity);
        _entitySyncService.UpdateEntityState(gameEntity);
        scene.RebuildHierarchy();
        return gameEntity;
    }

    private GameEntity? WaitForCreatedEntity(string name, System.Collections.Generic.HashSet<int> existingIds)
    {
        var scene = _sceneManagement.CurrentScene.Value;
        if (scene == null) return null;

        for (var i = 0; i < 120; i++)
        {
            var entity = TryCreateSyncedEntity(name, existingIds);
            if (entity != null) return entity;

            Task.Delay(16).Wait();
        }

        return scene.Entities.FirstOrDefault(x =>
            !existingIds.Contains(x.Id) &&
            string.Equals(x.Name, name, StringComparison.Ordinal));
    }

    private GameEntity? TryCreateSyncedEntity(string name, System.Collections.Generic.HashSet<int> existingIds)
    {
        var scene = _sceneManagement.CurrentScene.Value;
        var entities = _entityApi.GetSceneEntities();
        if (scene == null || entities == null) return null;

        foreach (var engineEntity in entities.Entities)
        {
            if (existingIds.Contains(engineEntity.Id)) continue;

            var state = _entityApi.GetEntityData(engineEntity.Id);
            if (state == null || !string.Equals(state.Name, name, StringComparison.Ordinal)) continue;

            return CreateSyncedEntity(scene, engineEntity.Id);
        }

        return null;
    }

    private int GetChildInsertionIndex(GameEntity parent)
    {
        var scene = _sceneManagement.CurrentScene.Value;
        var parentNode = scene?.Hierarchy.GetNode(parent);
        return parentNode?.ChildNodes.Count() ?? 0;
    }

    private void SyncTransformBehaviourProperties(GameEntity entity)
    {
        var transformBehaviourId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        var transform = entity.GetBehaviour(transformBehaviourId);
        if (transform == null) return;

        transform.GetProperty(EngineBehavioursConstants.TRANSFORM_PARENT).Value = entity.Transform.Parent;
        transform.GetProperty(EngineBehavioursConstants.TRANSFORM_ORDER).Value = entity.Transform.Order;
    }

    private void ApplyRectTransformLayout(GameEntity entity, BehaviourComponent rectTransform, RectTransformLayoutData layout)
    {
        _rectTransformLayoutService.ApplyLayoutToEditor(rectTransform, layout);
        _entityApi.SetData(new SetEntityDataRequest
        {
            SceneId = entity.Id,
            Behaviours = new List<Dictionary<string, object?>>
            {
                new()
                {
                    { SetEntityDataRequest.REI_BEHAVIOUR_ID, rectTransform.Id },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, _rectTransformLayoutService.SerializeVector2(layout.AnchorMin) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, _rectTransformLayoutService.SerializeVector2(layout.AnchorMax) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, _rectTransformLayoutService.SerializeVector2(layout.Pivot) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, _rectTransformLayoutService.SerializeVector2(layout.AnchoredPosition) },
                    { EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, _rectTransformLayoutService.SerializeVector2(layout.SizeDelta) }
                }
            }
        });
    }
}
