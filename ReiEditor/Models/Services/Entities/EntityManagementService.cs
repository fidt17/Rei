using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Entities;

public class EntityManagementService : IEntityManagementService, IDisposable
{
    public event Action<GameEntity>? EntityCreatedEvent;
    public event Action<GameEntity>? EntityMovedEvent;
    public event Action<GameEntity>? EntityDeletedEvent;
	
    private bool _ignorePropertyChanges;
    
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
        
        _behaviourComponentsService.BehaviourPropertyChangedEvent += HandleEntityBehaviourPropertyChangedEvent;
    }

    public void Dispose()
    {
        _behaviourComponentsService.BehaviourPropertyChangedEvent -= HandleEntityBehaviourPropertyChangedEvent;
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
            if (e.Name == name) return;

            if (_playmodeService.IsPlaymodeActive.Value)
            {
                _engineApi.RenameEntity(e.Id, name);
                UpdateEntityStateFromEngine(e);
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
        
        var state =_engineApi.GetEntityData(e.Id);
        if (state == null) return;
        
        _ignorePropertyChanges = true;
        
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
                            var p = behaviour.GetProperty(propertyName);
                            p.Value = map;
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
        
        _ignorePropertyChanges = false;
    }
    
    private void HandleEntityBehaviourPropertyChangedEvent(EntityBehaviourPropertyChangeEventArgs args)
    {
        if (!_playmodeService.IsPlaymodeActive.Value) return;
        
        if (_ignorePropertyChanges) return;

        KeyValuePair<string, object?>? fillPropertyChangeSequence(List<SerializedProperty> hierarchy)
        {
            if (hierarchy.Count == 0) return null;

            var sp = hierarchy[0];
            hierarchy.RemoveAt(0);
            
            if (sp.Type == SerializedTypeEnum.Custom)
            {
                var pair = fillPropertyChangeSequence(hierarchy);
                
                return new KeyValuePair<string, object?>(sp.Name, new Dictionary<string, object?>
                {
                    { 
                        "Value", new Dictionary<string, object?>
                        {
                            {pair.Value.Key, pair.Value.Value}
                        }
                    }
                });
            }
            else
            {
                return new KeyValuePair<string, object?>(sp.Name, new Dictionary<string, object?>
                {
                    { "Value", sp.Value }
                });
            }
        }
        
        try
        {
            var request = new SetEntityDataRequest
            {
                SceneId = args.Entity.Id,
                Behaviours = new List<Dictionary<string, object?>>()
            };
            
            var behaviourData = new Dictionary<string, object?> { { "REI_BEHAVIOUR_ID", args.Component.Id } };
            request.Behaviours.Add(behaviourData);
            
            var propertyHierarchy = new List<SerializedProperty>();
            args.Property.FillPropertyHierarchy(propertyHierarchy);

            var changeSequence = fillPropertyChangeSequence(propertyHierarchy);
            if (changeSequence == null) return;
            
            behaviourData.Add(changeSequence.Value.Key, changeSequence.Value.Value);
            
            if (request.Behaviours.Count == 0) return;

            _engineApi.SetEntityData(request);
            //_logger.LogWarning($"Request: {JsonConvert.SerializeObject(request, Formatting.Indented)}");
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
    }
}