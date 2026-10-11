using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntityStateApplier : IEntityStateApplier
{
    public bool IsApplyingEngineState { get; private set; }
    
    private readonly ILogger<EntitySyncService> _logger;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly ISerializedPropertiesService _serializedPropertiesService;

    public EntityStateApplier(
        ILogger<EntitySyncService> logger,
        IBehaviourRegistry behaviourRegistry,
        IBehaviourComponentsService behaviourComponentsService,
        ISerializedPropertiesService serializedPropertiesService)
    {
        _logger = logger;
        _behaviourRegistry = behaviourRegistry;
        _behaviourComponentsService = behaviourComponentsService;
        _serializedPropertiesService = serializedPropertiesService;
    }

    public void Apply(GameEntity entity, GetEntityDataResponse state)
    {
        using var timing = ReiEditor.Utils.Diagnostics.EditorInteractionTiming.Measure("Entity.Apply", entity.Id);
        IsApplyingEngineState = true;
        try
        {
            entity.SetName(state.Name);
            var behaviourIds = new List<int>();
            var hasBehaviourResolutionErrors = false;

            foreach (var behaviourState in state.Behaviours)
            {
                try
                {
                    if (!behaviourState.TryGetValue("REI_TYPE", out var reiTypeValue)) continue;

                    var reiType = reiTypeValue as string;
                    if (string.IsNullOrWhiteSpace(reiType)) continue;

                    var behaviourId = _behaviourRegistry.GetIdByName(reiType);
                    if (behaviourId == null)
                    {
                        hasBehaviourResolutionErrors = true;
                        _logger.LogError($"Could not find behaviour by REI_TYPE: {reiType}");
                        continue;
                    }
                    behaviourIds.Add(behaviourId.Value);

                    // try to add new behaviour
                    var behaviour = entity.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
                    if (behaviour == null)
                    {
                        _behaviourComponentsService.AddComponent(entity, behaviourId.Value);
                        behaviour = entity.Behaviours.FirstOrDefault(x => x.Id == behaviourId);
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
                            if (behaviour.HasProperty(propertyName))
                            {
                                var p = behaviour.GetProperty(propertyName);
                                _serializedPropertiesService.ApplyValue(p, value);
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

            if (!hasBehaviourResolutionErrors)
            {
                // delete behaviours that no longer exist on this entity
                foreach (var behaviour in entity.Behaviours.ToList())
                {
                    if (behaviourIds.Contains(behaviour.Id)) continue;
                    _behaviourComponentsService.DeleteComponent(entity, behaviour);
                }
            }
        }
        finally
        {
            IsApplyingEngineState = false;
        }
    }
}