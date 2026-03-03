using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public static class MaterialShaderPropertyUtils
{
    public static SerializedProperty CreateSerializedProperty(ShaderUniformInfo uniform, object? rawValue)
    {
        return uniform.Type switch
        {
            ShaderUniformType.Float => CreateFloatProperty(uniform.Name, uniform.SourceType, rawValue),
            ShaderUniformType.Integer => CreateIntegerProperty(uniform.Name, uniform.SourceType, rawValue),
            ShaderUniformType.Color => CreateColorProperty(uniform.Name, rawValue),
            ShaderUniformType.Texture => CreateTextureProperty(uniform.Name, rawValue),
            _ => throw new ArgumentOutOfRangeException(nameof(uniform.Type), uniform.Type, null)
        };
    }

    public static IEnumerable<SerializedProperty> GetObservedProperties(ShaderUniformType uniformType, SerializedProperty property)
    {
        if (uniformType is ShaderUniformType.Float or ShaderUniformType.Integer)
        {
            yield return property;
            yield break;
        }

        if (property.Value is not Dictionary<string, SerializedProperty> map) yield break;
        foreach (var nested in map.Values)
        {
            yield return nested;
        }
    }

    public static object? ConvertSerializedPropertyToMaterialValue(ShaderUniformType uniformType, SerializedProperty property)
    {
        return uniformType switch
        {
            ShaderUniformType.Float => ConvertToFloat(property.Value, 0f),
            ShaderUniformType.Integer => ConvertToInt(property.Value, 0),
            ShaderUniformType.Color => BuildColorValue(property),
            ShaderUniformType.Texture => BuildTextureValue(property),
            _ => null
        };
    }

    private static SerializedProperty CreateFloatProperty(string propertyName, string sourceType, object? rawValue)
    {
        var value = ConvertToFloat(rawValue, 0f);
        return new SerializedProperty(propertyName, SerializedTypeEnum.Float, value, sourceType, null);
    }

    private static SerializedProperty CreateIntegerProperty(string propertyName, string sourceType, object? rawValue)
    {
        var value = ConvertToInt(rawValue, 0);
        return new SerializedProperty(propertyName, SerializedTypeEnum.Integer, value, sourceType, null);
    }

    private static SerializedProperty CreateColorProperty(string propertyName, object? rawValue)
    {
        var root = new SerializedProperty(propertyName, SerializedTypeEnum.Custom, null, "Color", null);

        var r = ConvertToFloat(GetNestedValue(rawValue, "r"), 0f);
        var g = ConvertToFloat(GetNestedValue(rawValue, "g"), 0f);
        var b = ConvertToFloat(GetNestedValue(rawValue, "b"), 0f);
        var a = ConvertToFloat(GetNestedValue(rawValue, "a"), 1f);

        root.Value = new Dictionary<string, SerializedProperty>
        {
            ["r"] = new SerializedProperty("r", SerializedTypeEnum.Float, r, "float", root),
            ["g"] = new SerializedProperty("g", SerializedTypeEnum.Float, g, "float", root),
            ["b"] = new SerializedProperty("b", SerializedTypeEnum.Float, b, "float", root),
            ["a"] = new SerializedProperty("a", SerializedTypeEnum.Float, a, "float", root)
        };

        return root;
    }

    private static SerializedProperty CreateTextureProperty(string propertyName, object? rawValue)
    {
        var root = new SerializedProperty(propertyName, SerializedTypeEnum.Custom, null, "AssetRef<Texture>", null, "Texture");

        var textureAssetId = ConvertToString(GetNestedValue(rawValue, "Id"), "");
        root.Value = new Dictionary<string, SerializedProperty>
        {
            ["Id"] = new SerializedProperty("Id", SerializedTypeEnum.String, textureAssetId, "std::string", root)
        };

        return root;
    }

    private static Dictionary<string, object?> BuildColorValue(SerializedProperty property)
    {
        return new Dictionary<string, object?>
        {
            ["r"] = ConvertToFloat(GetNestedPropertyValue(property, "r"), 0f),
            ["g"] = ConvertToFloat(GetNestedPropertyValue(property, "g"), 0f),
            ["b"] = ConvertToFloat(GetNestedPropertyValue(property, "b"), 0f),
            ["a"] = ConvertToFloat(GetNestedPropertyValue(property, "a"), 1f)
        };
    }

    private static Dictionary<string, object?> BuildTextureValue(SerializedProperty property)
    {
        return new Dictionary<string, object?>
        {
            ["Id"] = ConvertToString(GetNestedPropertyValue(property, "Id"), "")
        };
    }

    private static object? GetNestedPropertyValue(SerializedProperty property, string nestedName)
    {
        if (property.Value is not Dictionary<string, SerializedProperty> nested) return null;
        return nested.TryGetValue(nestedName, out var nestedProperty) ? nestedProperty.Value : null;
    }

    private static object? GetNestedValue(object? value, string nestedName)
    {
        if (value == null) return null;
        value = UnwrapValue(value);
        if (value is string stringValue && nestedName.Equals("Id", StringComparison.OrdinalIgnoreCase)) return stringValue;

        if (value is JArray jArray && (nestedName.Equals("r", StringComparison.OrdinalIgnoreCase) ||
                                       nestedName.Equals("g", StringComparison.OrdinalIgnoreCase) ||
                                       nestedName.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                                       nestedName.Equals("a", StringComparison.OrdinalIgnoreCase)))
        {
            var index = nestedName.ToLowerInvariant() switch
            {
                "r" => 0,
                "g" => 1,
                "b" => 2,
                "a" => 3,
                _ => -1
            };

            if (index >= 0 && index < jArray.Count)
            {
                return UnwrapValue(jArray[index]);
            }
        }

        if (value is IEnumerable enumerable &&
            value is not string &&
            value is not JObject &&
            value is not JToken &&
            value is not IDictionary &&
            value is not IDictionary<string, object?> &&
            value is not Dictionary<string, object?> &&
            value is not Dictionary<string, SerializedProperty>)
        {
            var items = enumerable.Cast<object?>().ToList();
            if (items.Count > 0 && (nestedName.Equals("r", StringComparison.OrdinalIgnoreCase) ||
                                    nestedName.Equals("g", StringComparison.OrdinalIgnoreCase) ||
                                    nestedName.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                                    nestedName.Equals("a", StringComparison.OrdinalIgnoreCase)))
            {
                var index = nestedName.ToLowerInvariant() switch
                {
                    "r" => 0,
                    "g" => 1,
                    "b" => 2,
                    "a" => 3,
                    _ => -1
                };

                if (index >= 0 && index < items.Count)
                {
                    return UnwrapValue(items[index]);
                }
            }
        }

        if (value is Dictionary<string, object?> rawDictionary)
        {
            foreach (var (key, nestedValue) in rawDictionary)
            {
                if (key.Equals(nestedName, StringComparison.OrdinalIgnoreCase))
                {
                    return UnwrapValue(nestedValue);
                }

                if ((nestedName.Equals("r", StringComparison.OrdinalIgnoreCase) && key.Equals("x", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("g", StringComparison.OrdinalIgnoreCase) && key.Equals("y", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("b", StringComparison.OrdinalIgnoreCase) && key.Equals("z", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("a", StringComparison.OrdinalIgnoreCase) && key.Equals("w", StringComparison.OrdinalIgnoreCase)))
                {
                    return UnwrapValue(nestedValue);
                }
            }
            return null;
        }

        if (value is Dictionary<string, SerializedProperty> serializedDictionary)
        {
            foreach (var (key, nestedProperty) in serializedDictionary)
            {
                if (key.Equals(nestedName, StringComparison.OrdinalIgnoreCase))
                {
                    return UnwrapValue(nestedProperty.Value);
                }
            }
            return null;
        }

        if (value is IDictionary genericDictionary)
        {
            foreach (DictionaryEntry entry in genericDictionary)
            {
                var key = entry.Key?.ToString() ?? "";
                if (key.Equals(nestedName, StringComparison.OrdinalIgnoreCase))
                {
                    return UnwrapValue(entry.Value);
                }
            }
        }

        if (value is JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                if (property.Name.Equals(nestedName, StringComparison.OrdinalIgnoreCase))
                {
                    return UnwrapValue(property.Value);
                }

                if ((nestedName.Equals("r", StringComparison.OrdinalIgnoreCase) && property.Name.Equals("x", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("g", StringComparison.OrdinalIgnoreCase) && property.Name.Equals("y", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("b", StringComparison.OrdinalIgnoreCase) && property.Name.Equals("z", StringComparison.OrdinalIgnoreCase)) ||
                    (nestedName.Equals("a", StringComparison.OrdinalIgnoreCase) && property.Name.Equals("w", StringComparison.OrdinalIgnoreCase)))
                {
                    return UnwrapValue(property.Value);
                }
            }
        }

        return null;
    }

    private static float ConvertToFloat(object? value, float defaultValue)
    {
        if (value is null) return defaultValue;
        value = UnwrapValue(value);
        if (value is JToken token) value = token.ToObject<object?>();
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal dec) return (float)dec;

        var text = value?.ToString();
        if (!string.IsNullOrWhiteSpace(text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedInvariant))
        {
            return parsedInvariant;
        }

        if (!string.IsNullOrWhiteSpace(text) && float.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static int ConvertToInt(object? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        value = UnwrapValue(value);
        if (value is JToken token) value = token.ToObject<object?>();
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is float f) return (int)f;
        if (value is double d) return (int)d;

        var text = value?.ToString();
        if (!string.IsNullOrWhiteSpace(text) && int.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static string ConvertToString(object? value, string defaultValue)
    {
        if (value is null) return defaultValue;
        value = UnwrapValue(value);
        if (value is JToken token) value = token.ToObject<object?>();
        if (value is string stringValue) return stringValue;

        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? defaultValue : text;
    }

    private static object? UnwrapValue(object? value)
    {
        if (value is JObject jObj && jObj.TryGetValue("Value", StringComparison.OrdinalIgnoreCase, out var nested))
        {
            return UnwrapValue(nested);
        }

        if (value is IDictionary<string, object?> dict && dict.TryGetValue("Value", out var dictNested))
        {
            return UnwrapValue(dictNested);
        }

        if (value is JToken token && token.Type == JTokenType.Object)
        {
            var valueNode = token["Value"];
            if (valueNode != null)
            {
                return UnwrapValue(valueNode);
            }
        }

        return value;
    }
}
