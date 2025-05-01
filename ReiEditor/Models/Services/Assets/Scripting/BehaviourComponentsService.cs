using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Scripting;

public class BehaviourComponentsService : IBehaviourComponentsService
{
    private readonly ILogger<BehaviourComponentsService> _logger;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;

    public BehaviourComponentsService(
        ILogger<BehaviourComponentsService> logger,
        IBehaviourRegistry behaviourRegistry,
        ISerializableObjectsRegistry serializableObjectsRegistry)
    {
        _logger = logger;
        _behaviourRegistry = behaviourRegistry;
        _serializableObjectsRegistry = serializableObjectsRegistry;
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
            _logger.LogError($"{e} already has a component {componentInfo.BehaviourId}:{componentInfo.ObjectName}");
            return false;
        }

        var component = new BehaviourComponent(behaviourId);
        foreach (var sp in componentInfo.SerializedProperties)
        {
            component.AddProperty(CreateSerializedProperty(sp.Key, sp.Value));
        }
        
        SetupCustomBehaviourValues(component);
        
        e.AddBehaviour(component);
        
        return true;
    }

    public void AddComponent(GameEntity e, string name)
    {
        var id = _behaviourRegistry.GetIdByName(name);
        if (id == null) throw new Exception($"Could not find behaviour with name {name}");

        AddComponent(e, id.Value);
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
            // DELETE INVALID BEHAVIOUR
            if (!_behaviourRegistry.TryGetById(component.Id, out var componentInfo))
            {
                e.DeleteBehaviour(component);
                continue;
            }
            
            // ADD NEW PROPERTIES
            foreach (var sp in componentInfo.SerializedProperties)
            {
                if (!component.HasProperty(sp.Key))
                {
                    component.AddProperty(CreateSerializedProperty(sp.Key, sp.Value));
                }

                ParseNestedProperties(component.GetProperty(sp.Key));
            }

            // UPDATE PROPERTIES WITH NEW TYPES
            var cachedProperties = new Dictionary<string, SerializedProperty>(component.Properties);
            foreach (var sp in cachedProperties)
            {
                if (!componentInfo.SerializedProperties.TryGetValue(sp.Key, out var propertyType)) continue;
                
                if (sp.Value.Type != propertyType.Type || sp.Value.SourceType != propertyType.SourceType)
                {
                    component.RemoveProperty(sp.Key);
                    component.AddProperty(CreateSerializedProperty(sp.Key, propertyType));
                }
            }
        }
    }

    private SerializedProperty CreateSerializedProperty(string name, SerializableObjectInfo.SerializedPropertyData propertyData)
    {
        var propertyValue = propertyData.Type.ParseDefaultValue(propertyData.DefaultValue);
        var property = new SerializedProperty(name, propertyData.Type, propertyValue, propertyData.SourceType);
        if (property.Type != SerializedTypeEnum.Custom) return property;
        
        var nestedPropertyData = _serializableObjectsRegistry.GetObject(property.SourceType);
        if (nestedPropertyData == null)
        {
            _logger.LogError($"Could not find serializable object info for property {name} {propertyData.SourceType}");
            return property;
        }

        var nestedData = new Dictionary<string, SerializedProperty>();
        foreach (var serializedPropertyData in nestedPropertyData.SerializedProperties)
        {
            nestedData.Add(serializedPropertyData.Key, CreateSerializedProperty(serializedPropertyData.Key, serializedPropertyData.Value));
        }
        property.Value = nestedData;

        return property;
    }

    private SerializedProperty ParseSerializedProperty(string name, JToken jObject)
    {
        var type = jObject[nameof(SerializedProperty.Type)].ToObject<SerializedTypeEnum>();
        var value = jObject[nameof(SerializedProperty.Value)].ToObject<object>();
        var sourceType = jObject[nameof(SerializedProperty.SourceType)].ToObject<string>();
        var property = new SerializedProperty(name, type, value, sourceType);
        
        ParseNestedProperties(property);
        
        return property;
    }

    private void ParseNestedProperties(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Custom) return;

        var childObjects = new List<(string, JToken)>();

        if (property.Value is JObject jObject)
        {
            foreach (var keyValuePair in jObject)
            {
                childObjects.Add((keyValuePair.Key, keyValuePair.Value));
            }
        }

        if (childObjects.Count == 0) return;

        var requiredProperties = _serializableObjectsRegistry.GetObject(property.SourceType).SerializedProperties;

        var parsedValue = new Dictionary<string, SerializedProperty>();
        foreach (var token in childObjects)
        {
            parsedValue.Add(token.Item1, ParseSerializedProperty(token.Item1, token.Item2));
        }
            
        foreach (var requiredProperty in requiredProperties)
        {
            var targetProperty = parsedValue.FirstOrDefault(x => x.Value.Name == requiredProperty.Key).Value;
            if (targetProperty == null || targetProperty.SourceType != requiredProperty.Value.SourceType)
            {
                if (targetProperty != null)
                {
                    parsedValue.Remove(targetProperty.Name);
                }
                parsedValue.Add(requiredProperty.Key, CreateSerializedProperty(requiredProperty.Key, requiredProperty.Value));
            }
        }

        property.Value = parsedValue;
    }

    private void SetupCustomBehaviourValues(BehaviourComponent component)
    {
        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursUtility.TRANSFORM))
        {
            if (component.GetProperty(EngineBehavioursUtility.TRANSFORM_SCALE).Value is not Dictionary<string, SerializedProperty> scaleValue)
            {
                _logger.LogError("Could not find scale property on transform component");
                return;
            }
            
            scaleValue["x"].Value = 1;
            scaleValue["y"].Value = 1;
            scaleValue["z"].Value = 1;
        }
    }
}