using System;
using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

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
                var e = new GameEntity(s.AllocateEntityId(), name);
                AddBehaviour(e, _behaviourRegistry.GetIdByName("Transform")!.Value);
            
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
        var scene = _sceneManagement.CurrentScene.Value;
        if (scene == null) return;
        
        try
        {
            scene.MoveEntity(e, parent, idx);
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
                s.DeleteEntity(e);
            }
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }
}