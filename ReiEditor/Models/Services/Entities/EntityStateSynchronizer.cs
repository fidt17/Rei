using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Selection;
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

public class EntityStateSynchronizer : IEntityStateSynchronizer, IDisposable
{
    private bool _ignorePropertyChanges;
    private CancellationTokenSource? _sceneUpdateTaskCTS;
    
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ISelectionService _selectionService;
    private readonly ILogger<EntityStateSynchronizer> _logger;
    private readonly ISceneManagementService _sceneManagement;
    
    public EntityStateSynchronizer(
        IBehaviourRegistry behaviourRegistry,
        IEntityApi entityApi,
        ISelectionService selectionService,
        ILogger<EntityStateSynchronizer> logger,
        ISceneManagementService sceneManagement, 
        IEngineRunner engineRunner, 
        IBehaviourComponentsService behaviourComponentsService)
    {
        _behaviourRegistry = behaviourRegistry;
        _entityApi = entityApi;
        _selectionService = selectionService;
        _logger = logger;
        _sceneManagement = sceneManagement;
        _engineRunner = engineRunner;
        _behaviourComponentsService = behaviourComponentsService;

        _behaviourComponentsService.BehaviourPropertyChangedEvent += HandleEntityBehaviourPropertyChangedEvent;
        _engineRunner.IsActive.Subscribe(HandleEngineRunningValueChangedEvent, invoke: false);
    }
    
    public void Dispose()
    {
        _behaviourComponentsService.BehaviourPropertyChangedEvent -= HandleEntityBehaviourPropertyChangedEvent;
        _engineRunner.IsActive.Unsubscribe(HandleEngineRunningValueChangedEvent);
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
            //_logger.LogWarning($"Scene entities: {JsonConvert.SerializeObject(entities)}");
            
            foreach (var e in entities.Entities)
            {
                var gameEntity = currentSceneEntities.Find(x => x.Id == e.Id);
                // Create missing entities
                if (gameEntity == null)
                {
                    gameEntity = new GameEntity(e.Id, $"Entity {e}");
                    scene.AddEntity(gameEntity);
                    UpdateEntityState(gameEntity);
                    //_logger.LogWarning($"Add new entity: {entityId}");
                }
                
                // Update selection 
                UpdateEntitySelection(e, gameEntity);
            }
            
            // Delete invalid entities
            foreach (var gameEntity in currentSceneEntities.Where(x => !entities.Entities.Exists(y => y.Id == x.Id)))
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

    private void UpdateEntitySelection(GetSceneEntitiesResponse.SceneEntitiesResponseEntity e, GameEntity gameEntity)
    {
        if (e.IsSelected && !_selectionService.IsEntitySelected(gameEntity))
        {
            _selectionService.Select(gameEntity, sendToEngine: false);
        }
        else if (!e.IsSelected && _selectionService.IsEntitySelected(gameEntity))
        {
            _selectionService.ResetSelection(sendToEngine: false);
        }
    }
}