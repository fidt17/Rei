using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization;

public sealed class SerializedPropertiesService : ISerializedPropertiesService
{
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly ILogger<SerializedPropertiesService> _logger;

    public SerializedPropertiesService(ISerializableObjectsRegistry serializableObjectsRegistry, ILogger<SerializedPropertiesService> logger)
    {
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _logger = logger;
    }

    public SerializedProperty Create(string name, SerializableObjectInfo.SerializedPropertyData propertyData, SerializedProperty? parentProperty)
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
            nestedData.Add(serializedPropertyData.Key, Create(serializedPropertyData.Key, serializedPropertyData.Value, property));
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

    private SerializedProperty ParseNestedPropertyValue(string name, JToken token, SerializedProperty parentProperty, SerializableObjectInfo.SerializedPropertyData propertyData)
    {
        if (token is JObject tokenObject
            && tokenObject[nameof(SerializedProperty.Type)] != null
            && tokenObject[nameof(SerializedProperty.Value)] != null
            && tokenObject[nameof(SerializedProperty.SourceType)] != null)
        {
            return ParseSerializedProperty(name, tokenObject, parentProperty);
        }

        var property = Create(name, propertyData, parentProperty);
        ApplyValue(property, token.ToObject<object?>());
        return property;
    }

    public void Refresh(SerializedProperty property)
    {
        ParseNestedProperties(property);
    }

    private void ParseNestedProperties(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            ParseCollectionProperties(property);
            return;
        }

        if (property.Type != SerializedTypeEnum.Custom) return;

        var serializableObject = _serializableObjectsRegistry.GetObject(property.SourceType);
        if (serializableObject == null)
        {
            _logger.LogError($"Could not find serializable object info for property {property.Name} {property.SourceType} {property.Type}");
            return;
        }

        var requiredProperties = serializableObject.SerializedProperties;
        var parsedValue = property.Value is Dictionary<string, SerializedProperty> existingProperties
            ? new Dictionary<string, SerializedProperty>(existingProperties)
            : null;
        if (parsedValue == null)
        {
            parsedValue = new Dictionary<string, SerializedProperty>();
            if (property.Value is JObject jObject)
            {
                foreach (var (name, token) in jObject)
                {
                    if (!requiredProperties.TryGetValue(name, out var propertyData)) continue;
                    parsedValue.Add(name, ParseNestedPropertyValue(name, token!, property, propertyData));
                }
            }
        }
        foreach (var propertyName in parsedValue.Keys.Where(name => !requiredProperties.ContainsKey(name)).ToList())
        {
            parsedValue.Remove(propertyName);
        }
            
        foreach (var requiredProperty in requiredProperties)
        {
            if (!parsedValue.TryGetValue(requiredProperty.Key, out var targetProperty))
            {
                parsedValue.Add(requiredProperty.Key, Create(requiredProperty.Key, requiredProperty.Value, property));
            }
            else if (targetProperty.Type != requiredProperty.Value.Type || targetProperty.SourceType != requiredProperty.Value.SourceType)
            {
                var migratedProperty = Create(requiredProperty.Key, requiredProperty.Value, property);
                TryMigrateCompatibleCustomPropertyValue(targetProperty, migratedProperty);

                parsedValue[requiredProperty.Key] = migratedProperty;
            }
            else
            {
                targetProperty.SetTemplateTypeName(requiredProperty.Value.TemplateTypeName);
                Refresh(targetProperty);
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


    public void ApplyValue(SerializedProperty property, object? value)
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

    private SerializedProperty CreateCollectionItemProperty(SerializedProperty parentProperty, int index, SerializedTypeEnum itemType, string itemSourceType, string? itemTemplateTypeName, object? itemValue)
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
            if (property.Value is List<SerializedProperty> existingItems)
            {
                foreach (var item in existingItems) Refresh(item);
            }
            else
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

}
