using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Assets.Behaviours.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Behaviours;

public class BehaviourComponentsService : IBehaviourComponentsService
{
    private readonly ILogger<BehaviourComponentsService> _logger;
    private readonly IBehaviourRegistry _behaviourRegistry;

    public BehaviourComponentsService(ILogger<BehaviourComponentsService> logger, IBehaviourRegistry behaviourRegistry)
    {
        _logger = logger;
        _behaviourRegistry = behaviourRegistry;
    }

    public bool AddComponent(GameEntity e, int behaviourId)
    {
        if (!_behaviourRegistry.TryGetById(behaviourId, out var componentInfo))
        {
            _logger.LogException(new UnregisteredBehaviourException(behaviourId));
            return false;
        }
        
        if (e.HasComponent(behaviourId))
        {
            _logger.LogError($"{e} already has a component {componentInfo.BehaviourId}:{componentInfo.BehaviourName}");
            return false;
        }

        var component = new BehaviourComponent(behaviourId);
        foreach (var sp in componentInfo.SerializedProperties)
        {
            component.AddProperty(new SerializedProperty(sp.Key, sp.Value, sp.Value.GetDefaultValue()));
        }
        
        e.AddBehaviour(component);
        
        return true;
    }

    public bool DeleteComponent(GameEntity e, BehaviourComponent component)
    {
        if (!e.HasBehaviour(component))
        {
            _logger.LogError($"Cannot delete component {component.Id} from {e}. Entity does not have one.");
            return false;
        }
        
        e.DeleteBehaviour(component);
        return true;
    }

    public void RefreshComponents(GameEntity e)
    {
        var behaviours = e.Behaviours.ToList();
        
        foreach (var component in behaviours)
        {
            if (!_behaviourRegistry.TryGetById(component.Id, out var componentInfo))
            {
                e.DeleteBehaviour(component);
                continue;
            }
            
            foreach (var sp in componentInfo.SerializedProperties)
            {
                if (!component.HasProperty(sp.Key))
                {
                    component.AddProperty(new SerializedProperty(sp.Key, sp.Value, sp.Value.GetDefaultValue()));
                }
            }

            var cachedProperties = new Dictionary<string, SerializedProperty>(component.Properties);
            foreach (var sp in cachedProperties)
            {
                if (!componentInfo.SerializedProperties.TryGetValue(sp.Key, out var propertyType)) continue;
                
                if (sp.Value.Type != propertyType)
                {
                    component.RemoveProperty(sp.Key);
                    component.AddProperty(new SerializedProperty(sp.Key, propertyType, propertyType.GetDefaultValue()));
                }
            }
        }
    }
}