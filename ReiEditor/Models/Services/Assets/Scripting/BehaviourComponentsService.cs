using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

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

        foreach (var requiredComponentName in componentInfo.RequiredComponentNames)
        {
            var requiredBehaviourId = _behaviourRegistry.GetIdByName(requiredComponentName);
            if (requiredBehaviourId == null)
            {
                _logger.LogError($"Could not find required component {requiredComponentName} for {componentInfo.ObjectName}");
                continue;
            }

            if (e.HasComponent(requiredBehaviourId.Value)) continue;
            AddComponent(e, requiredBehaviourId.Value);
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

        if (TryGetRequiringComponent(e, component.Id, out var requiringComponentName))
        {
            _logger.LogError($"Cannot delete component {component.Id} from {e}. {requiringComponentName} requires it.");
            return false;
        }

        e.DeleteBehaviour(component);
        return true;
    }

    public bool TryGetRequiringComponent(GameEntity e, int requiredBehaviourId, out string requiringComponentName)
    {
        requiringComponentName = "";
        if (!_behaviourRegistry.TryGetById(requiredBehaviourId, out var requiredBehaviourInfo)) return false;

        foreach (var behaviour in e.Behaviours)
        {
            if (behaviour.Id == requiredBehaviourId) continue;
            if (!_behaviourRegistry.TryGetById(behaviour.Id, out var behaviourInfo)) continue;
            if (!behaviourInfo.RequiredComponentNames.Contains(requiredBehaviourInfo.ObjectName)) continue;

            requiringComponentName = behaviourInfo.ObjectName;
            return true;
        }

        return false;
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
                else if (sp.Value.TemplateTypeName == null)
                {
                    sp.Value.SetTemplateTypeName(propertyType.TemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(propertyType.SourceType));
                }
            }
        }
    }

    private SerializedProperty CreateSerializedProperty(string name, SerializableObjectInfo.SerializedPropertyData propertyData, SerializedProperty? parentProperty)
    {
        var templateTypeName = propertyData.TemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(propertyData.SourceType);
        var property = new SerializedProperty(
            name,
            propertyData.Type,
            CreateDefaultValue(propertyData),
            propertyData.SourceType,
            parentProperty,
            templateTypeName,
            propertyData.ItemType,
            propertyData.ItemSourceType,
            propertyData.ItemTemplateTypeName);

        if (property.Type == SerializedTypeEnum.Collection)
        {
            property.Value = new List<SerializedProperty>();
            return property;
        }

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
        var templateTypeName = SourceFilesUtility.GetTemplateTypeName(sourceType);
        var property = new SerializedProperty(
            name,
            type,
            value,
            sourceType,
            parentProperty,
            templateTypeName,
            GetCollectionItemType(type, templateTypeName),
            type == SerializedTypeEnum.Collection ? templateTypeName : null,
            type == SerializedTypeEnum.Collection && templateTypeName != null ? SourceFilesUtility.GetTemplateTypeName(templateTypeName) : null);
        
        ParseNestedProperties(property);
        
        return property;
    }

    private SerializedProperty ParseNestedPropertyValue(
        string name,
        JToken token,
        SerializedProperty parentProperty,
        SerializableObjectInfo.SerializedPropertyData propertyData)
    {
        if (token is JObject tokenObject
            && tokenObject[nameof(SerializedProperty.Type)] != null
            && tokenObject[nameof(SerializedProperty.Value)] != null
            && tokenObject[nameof(SerializedProperty.SourceType)] != null)
        {
            return ParseSerializedProperty(name, tokenObject, parentProperty);
        }

        var property = CreateSerializedProperty(name, propertyData, parentProperty);
        ApplySerializedValue(property, token.ToObject<object?>());
        return property;
    }

    private void ParseNestedProperties(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            ParseCollectionProperties(property);
            return;
        }

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

        var serializableObject = _serializableObjectsRegistry.GetObject(property.SourceType);
        if (serializableObject == null)
        {
            _logger.LogError($"Could not find serializable object info for property {property.Name} {property.SourceType} {property.Type}");
            return;
        }

        var requiredProperties = serializableObject.SerializedProperties;

        var parsedValue = new Dictionary<string, SerializedProperty>();
        foreach (var token in childObjects)
        {
            if (!requiredProperties.TryGetValue(token.Item1, out var propertyData)) continue;

            parsedValue.Add(token.Item1, ParseNestedPropertyValue(token.Item1, token.Item2, property, propertyData));
        }
            
        foreach (var requiredProperty in requiredProperties)
        {
            var targetProperty = parsedValue.FirstOrDefault(x => x.Value.Name == requiredProperty.Key).Value;
            if (targetProperty == null)
            {
                parsedValue.Add(requiredProperty.Key, CreateSerializedProperty(requiredProperty.Key, requiredProperty.Value, property));
            }
            else if (targetProperty.SourceType != requiredProperty.Value.SourceType)
            {
                var migratedProperty = CreateSerializedProperty(requiredProperty.Key, requiredProperty.Value, property);
                TryMigrateCompatibleCustomPropertyValue(targetProperty, migratedProperty);

                parsedValue.Remove(targetProperty.Name);
                parsedValue.Add(requiredProperty.Key, migratedProperty);
            }
            else
            {
                targetProperty.SetTemplateTypeName(requiredProperty.Value.TemplateTypeName);
            }
        }

        property.Value = parsedValue;
    }

    private static void TryMigrateCompatibleCustomPropertyValue(SerializedProperty sourceProperty, SerializedProperty targetProperty)
    {
        if (!AreCompatibleVectorTypes(sourceProperty.SourceType, targetProperty.SourceType)) return;
        if (sourceProperty.Value is not Dictionary<string, SerializedProperty> sourceProperties) return;
        if (targetProperty.Value is not Dictionary<string, SerializedProperty> targetProperties) return;

        foreach (var propertyName in new[] { "x", "y", "z" })
        {
            if (!sourceProperties.TryGetValue(propertyName, out var source)) continue;
            if (!targetProperties.TryGetValue(propertyName, out var target)) continue;

            target.Value = source.Value;
        }
    }

    private static bool AreCompatibleVectorTypes(string sourceType, string targetType)
    {
        return IsVectorType(sourceType) && IsVectorType(targetType);
    }

    private static bool IsVectorType(string sourceType)
    {
        var baseTypeName = SerializedTypeNameParser.GetBaseTypeName(sourceType);
        return baseTypeName is "Vector2" or "Vector3";
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

            if (property.Type == SerializedTypeEnum.Collection)
            {
                property.ValueChangedEvent += _ => BehaviourPropertyChangedEvent?.Invoke(new EntityBehaviourPropertyChangeEventArgs(entity, component, property));
            }
        }
        else if (property.Value is List<SerializedProperty> valueList)
        {
            foreach (var nested in valueList)
            {
                SubscribeToPropertyChange(entity, component, nested);
            }

            property.ValueChangedEvent += _ => BehaviourPropertyChangedEvent?.Invoke(new EntityBehaviourPropertyChangeEventArgs(entity, component, property));
        }
        else
        {
            property.ValueChangedEvent += _ => BehaviourPropertyChangedEvent?.Invoke(new EntityBehaviourPropertyChangeEventArgs(entity, component, property));
        }
    }

    public void ApplySerializedValue(SerializedProperty property, object? value)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            ApplyCollectionValue(property, value);
            return;
        }

        if (property.Type == SerializedTypeEnum.Custom && value is JObject jObject)
        {
            property.SetValueWithoutTriggeringChangedEvent(jObject.ToDictionary());
            property.TriggerChangedEvent();
            return;
        }

        property.SetValueWithoutTriggeringChangedEvent(value!);
        property.TriggerChangedEvent();
    }

    private void ApplyCollectionValue(SerializedProperty property, object? value)
    {
        if (value is not JArray valueArray)
        {
            if (property.Value is List<SerializedProperty>) return;

            property.SetValueWithoutTriggeringChangedEvent(new List<SerializedProperty>());
            property.NotifyStructureChanged();
            return;
        }

        var itemSourceType = property.ItemSourceType ?? property.TemplateTypeName;
        if (string.IsNullOrWhiteSpace(itemSourceType))
        {
            if (property.Value is List<SerializedProperty> existingItems && existingItems.Count == 0) return;

            property.SetValueWithoutTriggeringChangedEvent(new List<SerializedProperty>());
            property.NotifyStructureChanged();
            return;
        }

        if (property.Value is not List<SerializedProperty> items)
        {
            items = new List<SerializedProperty>();
            property.SetValueWithoutTriggeringChangedEvent(items);
        }

        var itemTemplateTypeName = property.ItemTemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(itemSourceType);
        var itemType = property.ItemType == SerializedTypeEnum.Invalid
            ? GetCollectionItemType(property.Type, itemSourceType)
            : property.ItemType;

        var structureChanged = items.Count != valueArray.Count;

        while (items.Count > valueArray.Count)
        {
            items.RemoveAt(items.Count - 1);
        }

        for (var index = 0; index < valueArray.Count; index++)
        {
            var itemValue = valueArray[index].ToObject<object?>();

            if (index >= items.Count)
            {
                items.Add(CreateCollectionItemProperty(property, index, itemType, itemSourceType, itemTemplateTypeName, itemValue));
                structureChanged = true;
                continue;
            }

            var itemProperty = items[index];
            if (itemProperty.Type != itemType || itemProperty.SourceType != itemSourceType)
            {
                items[index] = CreateCollectionItemProperty(property, index, itemType, itemSourceType, itemTemplateTypeName, itemValue);
                structureChanged = true;
                continue;
            }

            itemProperty.SetName($"[{index}]");
            itemProperty.SetTemplateTypeName(itemTemplateTypeName);
            ApplySerializedCollectionItemValue(itemProperty, itemValue);
        }

        if (structureChanged)
        {
            property.NotifyStructureChanged();
        }
    }

    private void ApplySerializedCollectionItemValue(SerializedProperty property, object? value)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            ApplyCollectionValue(property, value);
            return;
        }

        if (property.Type == SerializedTypeEnum.Custom && value is JObject jObject)
        {
            property.SetValueWithoutTriggeringChangedEvent(jObject.ToDictionary());
            ParseNestedProperties(property);
            return;
        }

        property.SetValueWithoutTriggeringChangedEvent(value!);
    }

    private SerializedProperty CreateCollectionItemProperty(
        SerializedProperty parentProperty,
        int index,
        SerializedTypeEnum itemType,
        string itemSourceType,
        string? itemTemplateTypeName,
        object? itemValue)
    {
        var itemProperty = new SerializedProperty(
            $"[{index}]",
            itemType,
            itemValue,
            itemSourceType,
            parentProperty,
            itemTemplateTypeName,
            itemType == SerializedTypeEnum.Collection ? GetCollectionItemType(itemType, itemTemplateTypeName) : SerializedTypeEnum.Invalid,
            itemType == SerializedTypeEnum.Collection ? itemTemplateTypeName : null,
            itemType == SerializedTypeEnum.Collection && itemTemplateTypeName != null ? SourceFilesUtility.GetTemplateTypeName(itemTemplateTypeName) : null);
        ParseNestedProperties(itemProperty);
        return itemProperty;
    }

    private object? CreateDefaultValue(SerializableObjectInfo.SerializedPropertyData propertyData)
    {
        if (propertyData.Type == SerializedTypeEnum.Collection)
        {
            return new List<SerializedProperty>();
        }

        if (propertyData.Type != SerializedTypeEnum.Enum)
        {
            return propertyData.Type.ParseDefaultValue(propertyData.DefaultValue);
        }

        var enumData = _serializableObjectsRegistry.GetEnum(propertyData.SourceType.Split("::").Last());
        if (enumData == null)
        {
            _logger.LogError($"Could not find serializable enum info for property {propertyData.SourceType}");
            return 0;
        }

        if (int.TryParse(propertyData.DefaultValue, out var enumInt))
        {
            return enumInt;
        }

        if (string.IsNullOrWhiteSpace(propertyData.DefaultValue))
        {
            return enumData.Options.Count > 0 ? enumData.Options.First().Value : 0;
        }

        return enumData.Options[propertyData.DefaultValue!.Split("::").Last()];
    }

    private void ParseCollectionProperties(SerializedProperty property)
    {
        if (property.Value is not JArray valueArray)
        {
            if (property.Value is not List<SerializedProperty>)
            {
                property.Value = new List<SerializedProperty>();
            }
            return;
        }

        var itemSourceType = property.ItemSourceType ?? property.TemplateTypeName;
        if (string.IsNullOrWhiteSpace(itemSourceType))
        {
            property.Value = new List<SerializedProperty>();
            return;
        }

        var itemTemplateTypeName = property.ItemTemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(itemSourceType);
        var itemType = property.ItemType == SerializedTypeEnum.Invalid
            ? GetCollectionItemType(property.Type, itemSourceType)
            : property.ItemType;

        var parsedItems = new List<SerializedProperty>();
        for (var index = 0; index < valueArray.Count; index++)
        {
            parsedItems.Add(CreateCollectionItemProperty(
                property,
                index,
                itemType,
                itemSourceType,
                itemTemplateTypeName,
                valueArray[index].ToObject<object?>()));
        }

        property.Value = parsedItems;
    }

    private SerializedTypeEnum GetCollectionItemType(SerializedTypeEnum propertyType, string? itemSourceType)
    {
        if (propertyType != SerializedTypeEnum.Collection || string.IsNullOrWhiteSpace(itemSourceType))
        {
            return SerializedTypeEnum.Invalid;
        }

        var typeName = SerializedTypeNameParser.NormalizeSourceType(itemSourceType);
        var baseTypeName = SerializedTypeNameParser.GetBaseTypeName(typeName);

        if (baseTypeName is "int" or "i32" or "u32")
        {
            return SerializedTypeEnum.Integer;
        }

        if (baseTypeName is "string")
        {
            return SerializedTypeEnum.String;
        }

        if (baseTypeName is "bool")
        {
            return SerializedTypeEnum.Boolean;
        }

        if (baseTypeName is "float" or "f32" or "double")
        {
            return SerializedTypeEnum.Float;
        }

        if (baseTypeName is "vector")
        {
            return SerializedTypeEnum.Collection;
        }

        if (_serializableObjectsRegistry.GetEnum(baseTypeName) != null)
        {
            return SerializedTypeEnum.Enum;
        }

        return SerializedTypeEnum.Custom;
    }

    private void SetupCustomBehaviourValues(BehaviourComponent component)
    {
        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM))
        {
            if (component.GetProperty(EngineBehavioursConstants.TRANSFORM_SCALE).Value is not Dictionary<string, SerializedProperty> scaleValue)
            {
                _logger.LogError("Could not find scale property on transform component");
                return;
            }
            
            scaleValue["x"].Value = 1;
            scaleValue["y"].Value = 1;
            scaleValue["z"].Value = 1;
            return;
        }

        if (component.Id == _behaviourRegistry.GetIdByName(EngineBehavioursConstants.MESH_RENDERER))
        {
            TrySetAssetRefId(
                component,
                EngineBehavioursConstants.MESH_RENDERER_MATERIAL,
                EngineBehavioursConstants.DEFAULT_ENGINE_SIMPLE_LIT_MATERIAL_ASSET_ID);
        }
    }

    private static void TrySetAssetRefId(BehaviourComponent component, string propertyName, string assetId)
    {
        if (!component.HasProperty(propertyName)) return;
        var property = component.GetProperty(propertyName);
        if (property.Value is not Dictionary<string, SerializedProperty> nestedProperties) return;
        if (!nestedProperties.TryGetValue(EngineBehavioursConstants.ASSET_REF_ID, out var idProperty)) return;

        idProperty.Value = assetId;
    }
}
