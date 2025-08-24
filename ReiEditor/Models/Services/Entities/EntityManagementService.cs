using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private bool _ignorePropertyChanges;
    private CancellationTokenSource? _sceneUpdateTaskCTS;
    
    private readonly ILogger<EntityManagementService> _logger;
    private readonly ISceneManagementService _sceneManagement;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public EntityManagementService(
        ILogger<EntityManagementService> logger,
        ISceneManagementService sceneManagement,
        IBehaviourComponentsService behaviourComponentsService,
        IEntityApi entityApi, 
        IBehaviourRegistry behaviourRegistry, 
        IEngineRunner engineRunner)
    {
        _logger = logger;
        _sceneManagement = sceneManagement;
        _behaviourComponentsService = behaviourComponentsService;
        _entityApi = entityApi;
        _behaviourRegistry = behaviourRegistry;
        _engineRunner = engineRunner;

        _behaviourComponentsService.BehaviourPropertyChangedEvent += HandleEntityBehaviourPropertyChangedEvent;
        
        _engineRunner.IsActive.Subscribe(HandleEngineRunningValueChangedEvent, invoke: false);
    }

    public void Dispose()
    {
        _behaviourComponentsService.BehaviourPropertyChangedEvent -= HandleEntityBehaviourPropertyChangedEvent;
        _engineRunner.IsActive.Unsubscribe(HandleEngineRunningValueChangedEvent);
    }

    public GameEntity? CreateEntity(string name)
    {
        try
        {
            if (!_engineRunner.IsActive.Value)
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
            if (string.IsNullOrEmpty(name)) throw new Exception($"Invalid entity name [{name}]");
            if (e.Name == name) return;

            if (_engineRunner.IsActive.Value)
            {
                _entityApi.Rename(e.Id, name);
                UpdateEntityState(e);
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
    
    public void UpdateEntityState(GameEntity e)
    {
        if (!_engineRunner.IsActive.Value) return;
        
        var state =_entityApi.GetEntityData(e.Id);
        //_logger.LogWarning($"State: {JsonConvert.SerializeObject(state, Formatting.Indented)}");
        if (state == null) return;
        
        _ignorePropertyChanges = true;
        
        e.SetName(state.Name);
        var behaviourIds = new List<int>();
        foreach (var behaviourState in state.Behaviours)
        {
            try
            {
                var reiType = (string)behaviourState["REI_TYPE"];
                var behaviourId = _behaviourRegistry.GetIdByName(reiType);
                if (behaviourId == null) throw new Exception($"Could not find behaviour by REI_TYPE: {reiType}");
                behaviourIds.Add(behaviourId.Value);
                
                // try to add new behaviour
                var behaviour = e.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
                if (behaviour == null)
                {
                    _behaviourComponentsService.AddComponent(e, behaviourId.Value);
                    behaviour = e.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
                    if (behaviour == null)
                    {
                        throw new Exception($"Entity is missing a behaviour with id={behaviourId}");
                    }
                }
                
                // update behaviour properties
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
        
        // delete behaviours that no longer exist on this entity
        foreach (var b in e.Behaviours.ToList())
        {
            if (behaviourIds.Contains(b.Id)) continue;
            _behaviourComponentsService.DeleteComponent(e, b);
        }
        
        _ignorePropertyChanges = false;
    }
    
    private void HandleEntityBehaviourPropertyChangedEvent(EntityBehaviourPropertyChangeEventArgs args)
    {
        if (!_engineRunner.IsActive.Value) return;
        
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
                            {pair!.Value.Key, pair.Value.Value}
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

            _entityApi.SetData(request);
            //_logger.LogWarning($"Request: {JsonConvert.SerializeObject(request, Formatting.Indented)}");
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
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
                    await Task.Delay(100, token);

                    UpdateSceneEntitiesFromEngine();
                }
                // ReSharper disable once FunctionNeverReturns
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
            
            var entities = _entityApi.GetSceneEntities();
            if (entities == null) return;

            var scene = _sceneManagement.CurrentScene.Value;
            if (scene == null) throw new Exception("Current scene is missing");

            var currentSceneEntities = scene.Entities.ToList();
            
            // Create missing entities
            foreach (var entityId in entities.Entities)
            {
                if (!currentSceneEntities.Exists(x => x.Id == entityId))
                {
                    var newEntity = new GameEntity(entityId, $"Entity {entityId}");
                    scene.AddEntity(newEntity);
                    UpdateEntityState(newEntity);
                    //_logger.LogWarning($"Add new entity: {entityId}");
                }
            }
            
            // Delete invalid entities
            foreach (var gameEntity in currentSceneEntities.Where(x => !entities.Entities.Contains(x.Id)))
            {
                scene.DeleteEntity(gameEntity);
                //_logger.LogWarning($"Delete invalid entity: {gameEntity.Name}");
            }
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
}