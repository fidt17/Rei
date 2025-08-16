using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Entities;

public class EntityManagementService : IEntityManagementService
{
    public event Action<GameEntity>? EntityCreatedEvent;
    public event Action<GameEntity>? EntityMovedEvent;
    public event Action<GameEntity>? EntityDeletedEvent;
	
    private readonly ILogger<EntityManagementService> _logger;
    private readonly ISceneManagementService _sceneManagement;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

    private readonly IPlaymodeService _playmodeService;
    private readonly IEngineApi _engineApi;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public EntityManagementService(
        ILogger<EntityManagementService> logger,
        ISceneManagementService sceneManagement,
        IBehaviourComponentsService behaviourComponentsService,
        IPlaymodeService playmodeService,
        IEngineApi engineApi, 
        IBehaviourRegistry behaviourRegistry)
    {
        _logger = logger;
        _sceneManagement = sceneManagement;
        _behaviourComponentsService = behaviourComponentsService;
        _playmodeService = playmodeService;
        _engineApi = engineApi;
        _behaviourRegistry = behaviourRegistry;
    }

    public GameEntity? CreateEntity(string name)
    {
        try
        {
            if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

            var s = _sceneManagement.CurrentScene.Value;
            var e = new GameEntity(s.AllocateEntityId(), name);
            _behaviourComponentsService.AddComponent(e, "Transform");
            
            s.AddEntity(e);

            EntityCreatedEvent?.Invoke(e);
            return e;
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
            if (string.IsNullOrEmpty(name)) throw new Exception($"Invalid entity name [{name}]");
            e.SetName(name);
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
            if (!scene.MoveEntity(e, parent, idx)) return;
            EntityMovedEvent?.Invoke(e);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void DeleteEntity(GameEntity e)
    {
        try
        {
            if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

            var s = _sceneManagement.CurrentScene.Value;
            s.DeleteEntity(e);

            EntityDeletedEvent?.Invoke(e);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void UpdateEntityStateFromEngine(GameEntity e)
    {
        if (!_playmodeService.IsPlaymodeActive.Value) return;
        
        var state =_engineApi.GetSceneEntityState(e.Id);
        if (state == null) return;
        
        e.SetName(state.Name);
        
        foreach (var behaviourState in state.Behaviours)
        {
            try
            {
                var reiType = (string)behaviourState["REI_TYPE"];
                var behaviourId = _behaviourRegistry.GetIdByName(reiType);
                if (behaviourId == null) throw new Exception($"Could not find behaviour by REI_TYPE: {reiType}");

                var behaviour = e.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
                if (behaviour == null) throw new Exception($"Entity is missing a behaviour with id={behaviourId}");
                
                foreach (var (propertyName, value) in behaviourState)
                {
                    try
                    {
                        var map = value is JObject jObject ? jObject.ToDictionary() : value;
                        
                        if (behaviour.HasProperty(propertyName))
                        {
                            behaviour.GetProperty(propertyName).Value = map;
                        }
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError($"Exception while parsing property({propertyName}) in behaviour state: {JsonConvert.SerializeObject(behaviourState, Formatting.Indented)}. \n{exception}");
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogError($"Exception while parsing behaviour state: {JsonConvert.SerializeObject(behaviourState, Formatting.Indented)}. \n{exception}");
            }
        }
    }
}