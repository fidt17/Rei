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
    public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent;

    private readonly HashSet<SerializedProperty> _subscribedProperties = new();
    
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
            var p = CreateSerializedProperty(sp.Key, sp.Value, null);
            component.AddProperty(p);
            SubscribeToPropertyChange(e, component, p);
        }
        
        SetupCustomBehaviourValues(component);
        
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
                    component.AddProperty(CreateSerializedProperty(sp.Key, sp.Value, null));
                }

                var p = component.GetProperty(sp.Key);
                ParseNestedProperties(p);
                SubscribeToPropertyChange(e, component, p);
            }

            // UPDATE PROPERTIES WITH NEW TYPES
            var cachedProperties = new Dictionary<string, SerializedProperty>(component.Properties);
            foreach (var sp in cachedProperties)
            {
                if (!componentInfo.SerializedProperties.TryGetValue(sp.Key, out var propertyType)) continue;
                
                if (sp.Value.Type != propertyType.Type || sp.Value.SourceType != propertyType.SourceType)
                {
                    component.RemoveProperty(sp.Key);
                    var p = CreateSerializedProperty(sp.Key, propertyType, null);
                    component.AddProperty(p);
                    SubscribeToPropertyChange(e, component, p);
                }
            }
        }
    }

    private SerializedProperty CreateSerializedProperty(string name, SerializableObjectInfo.SerializedPropertyData propertyData, SerializedProperty? parentProperty)
    {
        object? propertyValue;
        
        if (propertyData.Type == SerializedTypeEnum.Enum)
        {
            var enumData = _serializableObjectsRegistry.GetEnum(propertyData.SourceType.Split("::").Last());
            if (enumData == null)
            {
                _logger.LogError($"Could not find serializable enum info for property {name} {propertyData.SourceType}");
                propertyValue = 0;
            }
            else
            {
                if (int.TryParse(propertyData.DefaultValue, out var enumInt))
                {
                    propertyValue = enumInt;
                }
                else
                {
                    propertyValue = enumData.Options[propertyData.DefaultValue!.Split("::").Last()];
                }
            }
        }
        else
        {
            propertyValue = propertyData.Type.ParseDefaultValue(propertyData.DefaultValue);
        }
        
        var property = new SerializedProperty(name, propertyData.Type, propertyValue, propertyData.SourceType, parentProperty);
        
        if (property.Type != SerializedTypeEnum.Custom) return property;
        
        var nestedPropertyData = _serializableObjectsRegistry.GetObject(property.SourceType);
        if (nestedPropertyData == null)
        {
            _logger.LogError($"Could not find serializable object info for property {name} {propertyData.SourceType} {propertyData.Type}");
            return property;
        }

        var nestedData = new Dictionary<string, SerializedProperty>();
        foreach (var serializedPropertyData in nestedPropertyData.SerializedProperties)
        {
            nestedData.Add(serializedPropertyData.Key, CreateSerializedProperty(serializedPropertyData.Key, serializedPropertyData.Value, property));
        }
        property.Value = nestedData;

        return property;
    }

    private SerializedProperty ParseSerializedProperty(string name, JToken jObject, SerializedProperty? parentProperty)
    {
        var type = jObject[nameof(SerializedProperty.Type)]!.ToObject<SerializedTypeEnum>();
        var value = jObject[nameof(SerializedProperty.Value)]!.ToObject<object>();
        var sourceType = jObject[nameof(SerializedProperty.SourceType)]!.ToObject<string>() ?? "";
        var property = new SerializedProperty(name, type, value, sourceType, parentProperty);
        
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
                childObjects.Add((keyValuePair.Key, keyValuePair.Value)!);
            }
        }

        if (childObjects.Count == 0) return;

        var requiredProperties = _serializableObjectsRegistry.GetObject(property.SourceType)!.SerializedProperties;

        var parsedValue = new Dictionary<string, SerializedProperty>();
        foreach (var token in childObjects)
        {
            parsedValue.Add(token.Item1, ParseSerializedProperty(token.Item1, token.Item2, property));
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
                parsedValue.Add(requiredProperty.Key, CreateSerializedProperty(requiredProperty.Key, requiredProperty.Value, property));
            }
        }

        property.Value = parsedValue;
    }

    private void SubscribeToPropertyChange(GameEntity entity, BehaviourComponent component, SerializedProperty property)
    {
        if (!_subscribedProperties.Add(property)) return;

        if (property.Value is Dictionary<string, SerializedProperty> sp)
        {
            foreach (var nested in sp.Values)
            {
                SubscribeToPropertyChange(entity, component, nested);
            }
        }
        else
        {
            property.ValueChangedEvent += _ => BehaviourPropertyChangedEvent?.Invoke(new EntityBehaviourPropertyChangeEventArgs(entity, component, property));
        }
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