using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal static class McpValueConverter
{
    private const int MAX_DEPTH = 16;

    public static object? ToContractValue(object? value)
    {
        return ConvertValue(value, 0);
    }

    private static object? ConvertValue(object? value, int depth)
    {
        if (value == null) return null;
        if (depth >= MAX_DEPTH) return "<max-depth>";

        if (value is SerializedProperty property) return ConvertValue(property.Value, depth + 1);
        if (value is JToken token) return ConvertToken(token, depth + 1);
        if (value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal) return value;
        if (value is Enum enumValue) return enumValue.ToString();
        if (value is DateTime dateTime) return dateTime.ToString("O");
        if (value is DateTimeOffset dateTimeOffset) return dateTimeOffset.ToString("O");
        if (value is Guid or Uri) return value.ToString();

        if (value is IReadOnlyDictionary<string, SerializedProperty> propertyDictionary)
        {
            return propertyDictionary.ToDictionary(x => x.Key, x => ConvertValue(x.Value.Value, depth + 1));
        }

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key) continue;
                result[key] = ConvertValue(entry.Value, depth + 1);
            }

            return result;
        }

        if (value is IEnumerable enumerable)
        {
            var result = new List<object?>();
            foreach (var item in enumerable)
            {
                result.Add(ConvertValue(item, depth + 1));
            }

            return result;
        }

        try
        {
            return ConvertToken(JToken.FromObject(value), depth + 1);
        }
        catch
        {
            return value.ToString();
        }
    }

    private static object? ConvertToken(JToken token, int depth)
    {
        if (depth >= MAX_DEPTH) return "<max-depth>";

        return token.Type switch
        {
            JTokenType.Null or JTokenType.Undefined => null,
            JTokenType.Object => token.Children<JProperty>().ToDictionary(x => x.Name, x => ConvertToken(x.Value, depth + 1)),
            JTokenType.Array => token.Children().Select(x => ConvertToken(x, depth + 1)).ToList(),
            JTokenType.Integer => token.Value<long>(),
            JTokenType.Float => token.Value<double>(),
            JTokenType.Boolean => token.Value<bool>(),
            JTokenType.Date => token.Value<DateTime>().ToString("O"),
            JTokenType.Guid or JTokenType.Uri or JTokenType.TimeSpan => token.ToString(),
            _ => token.Value<string>() ?? token.ToString()
        };
    }
}
