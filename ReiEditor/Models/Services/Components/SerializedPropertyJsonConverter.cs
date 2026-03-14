using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

namespace ReiEditor.Models.Services.Components;

public class SerializedPropertyJsonConverter : JsonConverter<SerializedProperty>
{
    public override void WriteJson(JsonWriter writer, SerializedProperty? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        SerializeProperty(value).WriteTo(writer);
    }

    public override SerializedProperty? ReadJson(
        JsonReader reader,
        Type objectType,
        SerializedProperty? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        var token = JToken.Load(reader);
        if (token is not JObject propertyObject)
        {
            throw new JsonSerializationException($"Expected serialized property object but got {token.Type}");
        }

        return DeserializeProperty(propertyObject, parentProperty: null);
    }

    private static JObject SerializeProperty(SerializedProperty property)
    {
        return new JObject
        {
            [nameof(SerializedProperty.Value)] = SerializePropertyValue(property),
            [nameof(SerializedProperty.Name)] = property.Name,
            [nameof(SerializedProperty.Type)] = (int)property.Type,
            [nameof(SerializedProperty.SourceType)] = property.SourceType
        };
    }

    private static JToken SerializePropertyValue(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
        {
            var items = property.Value as List<SerializedProperty> ?? new List<SerializedProperty>();
            var array = new JArray();
            foreach (var item in items)
            {
                array.Add(SerializeCollectionItem(item));
            }

            return array;
        }

        if (property.Type == SerializedTypeEnum.Custom)
        {
            return SerializeCustomValue(property);
        }

        return property.Value == null ? JValue.CreateNull() : JToken.FromObject(property.Value);
    }

    private static JToken SerializeCollectionItem(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Custom)
        {
            return SerializeCustomValue(property);
        }

        if (property.Type == SerializedTypeEnum.Collection)
        {
            return SerializePropertyValue(property);
        }

        return property.Value == null ? JValue.CreateNull() : JToken.FromObject(property.Value);
    }

    private static JObject SerializeCustomValue(SerializedProperty property)
    {
        var value = property.Value as Dictionary<string, SerializedProperty>;
        var obj = new JObject();
        if (value == null) return obj;

        foreach (var nestedProperty in value.Values)
        {
            obj[nestedProperty.Name] = SerializeProperty(nestedProperty);
        }

        return obj;
    }

    private static SerializedProperty DeserializeProperty(JObject propertyObject, SerializedProperty? parentProperty)
    {
        var name = propertyObject[nameof(SerializedProperty.Name)]?.ToObject<string>() ?? string.Empty;
        var type = propertyObject[nameof(SerializedProperty.Type)]?.ToObject<SerializedTypeEnum>() ?? SerializedTypeEnum.Invalid;
        var sourceType = propertyObject[nameof(SerializedProperty.SourceType)]?.ToObject<string>() ?? string.Empty;
        var templateTypeName = SourceFilesUtility.GetTemplateTypeName(sourceType);
        var value = DeserializePropertyValue(type, propertyObject[nameof(SerializedProperty.Value)]);

        return new SerializedProperty(
            name,
            type,
            value,
            sourceType,
            parentProperty,
            templateTypeName,
            SerializedTypeEnum.Invalid,
            type == SerializedTypeEnum.Collection ? templateTypeName : null,
            type == SerializedTypeEnum.Collection && templateTypeName != null ? SourceFilesUtility.GetTemplateTypeName(templateTypeName) : null);
    }

    private static object? DeserializePropertyValue(SerializedTypeEnum type, JToken? valueToken)
    {
        if (valueToken == null) return null;

        if (type == SerializedTypeEnum.Collection && valueToken is JArray array)
        {
            var normalized = new JArray();
            foreach (var item in array)
            {
                normalized.Add(NormalizeCollectionItemToken(item));
            }

            return normalized;
        }

        if (type == SerializedTypeEnum.Custom && valueToken is JObject customObject)
        {
            return customObject;
        }

        return valueToken.ToObject<object?>();
    }

    private static JToken NormalizeCollectionItemToken(JToken token)
    {
        if (token is not JObject tokenObject) return token.DeepClone();

        if (tokenObject[nameof(SerializedProperty.Type)] == null ||
            tokenObject[nameof(SerializedProperty.Value)] == null ||
            tokenObject[nameof(SerializedProperty.SourceType)] == null)
        {
            return token.DeepClone();
        }

        return tokenObject[nameof(SerializedProperty.Value)]!.DeepClone();
    }
}
