using System;
using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Logging.Loggers;
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
    private readonly IEntityStateSynchronizer _entityStateSynchronizer;

    public EntityManagementService(
        ILogger<EntityManagementService> logger,
        ISceneManagementService sceneManagement,
        IBehaviourComponentsService behaviourComponentsService,
        IEntityApi entityApi, 
        IBehaviourRegistry behaviourRegistry, 
        IEngineRunner engineRunner, 
        IEntityStateSynchronizer entityStateSynchronizer)
    {
        _logger = logger;
        _sceneManagement = sceneManagement;
        _behaviourComponentsService = behaviourComponentsService;
        _entityApi = entityApi;
        _behaviourRegistry = behaviourRegistry;
        _engineRunner = engineRunner;
        _entityStateSynchronizer = entityStateSynchronizer;

    }

    public GameEntity? CreateEntity(string name)
    {
        try
        {
            if (_engineRunner.IsActive.Value)
            {
                _entityApi.CreateNewEntity(name);
            }
            else
            {
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
                e.GetBehaviour(transformBehaviourId)!.GetProperty("_order").Value = e.Transform.Order;
            
                s.AddEntity(e);
                return e;
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }

        return null;
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
                _entityStateSynchronizer.UpdateEntityState(e);
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
            _entityApi.SetEntityParent(e.Id, parent?.Id ?? 0, idx);
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

    public void InstantiateEntity(GameEntity sourceEntity, string? requestedName = null, bool includeChildren = true)
    {
        try
        {
            if (!_engineRunner.IsActive.Value)
            {
                throw new Exception("Instantiate entity requires engine to be running");
            }

            var baseName = string.IsNullOrWhiteSpace(requestedName) ? sourceEntity.Name : requestedName;
            var scene = _sceneManagement.CurrentScene.Value;
            var uniqueName = scene == null
                ? baseName
                : NamingUtils.GetUniqueName(baseName, scene.Entities.Select(x => x.Name));

            _entityApi.InstantiateEntity(new InstantiateEntityRequest
            {
                SourceEntityId = sourceEntity.Id,
                RequestedName = uniqueName,
                IncludeChildren = includeChildren
            });
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void DestroyEntity(GameEntity e)
    {
        try
        {
            if (_engineRunner.IsActive.Value)
            {
                _entityApi.DestroyEntity(e.Id);
            }
            else
            {
                if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

                var s = _sceneManagement.CurrentScene.Value;
                var entitiesToDestroy = EntityUtils.GetEntitiesForRecursiveDestroy(s, e);
                foreach (var entity in entitiesToDestroy)
                {
                    s.DeleteEntity(entity);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }
}
