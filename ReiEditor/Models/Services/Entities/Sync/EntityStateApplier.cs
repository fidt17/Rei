using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntityStateApplier
{
    private readonly ILogger<EntityStateSynchronizer> _logger;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

    public EntityStateApplier(
        ILogger<EntityStateSynchronizer> logger,
        IBehaviourRegistry behaviourRegistry,
        IBehaviourComponentsService behaviourComponentsService)
    {
        _logger = logger;
        _behaviourRegistry = behaviourRegistry;
        _behaviourComponentsService = behaviourComponentsService;
    }

    public bool Apply(GameEntity entity, GetEntityDataResponse state)
    {
        entity.SetName(state.Name);
        var behaviourIds = new List<int>();
        int? transformParent = null;
        int? transformOrder = null;
        var needsHierarchyRefresh = false;
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

                if (reiType == EngineBehavioursConstants.TRANSFORM)
                {
                    transformParent = EntityStateSyncUtility.TryReadInt(behaviourState, EngineBehavioursConstants.TRANSFORM_PARENT);
                    transformOrder = EntityStateSyncUtility.TryReadInt(behaviourState, EngineBehavioursConstants.TRANSFORM_ORDER);
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

        if (transformParent.HasValue && transformOrder.HasValue)
        {
            if (entity.Transform.Parent != transformParent.Value || entity.Transform.Order != transformOrder.Value)
            {
                entity.Transform.SetParent(transformParent.Value);
                entity.Transform.SetOrder(transformOrder.Value);
                needsHierarchyRefresh = true;
            }
        }

        return needsHierarchyRefresh;
    }
}
