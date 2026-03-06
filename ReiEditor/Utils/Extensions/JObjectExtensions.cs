using System;

namespace ReiEditor.Utils.Extensions;

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public static class JObjectExtensions
{
    public static Dictionary<string, object?> ToDictionary(this JObject json)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var property in json.Properties())
        {
            result[property.Name] = ConvertToken(property.Value);
        }
        
        return result;
    }

    private static object? ConvertToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                return ((JObject)token).ToDictionary();
                
            case JTokenType.Array:
                var list = new List<object?>();
                foreach (var item in (JArray)token)
                {
                    list.Add(ConvertToken(item));
                }
                return list;
                
            case JTokenType.Integer:
                return token.Value<long>();
                
            case JTokenType.Float:
                return token.Value<double>();
                
            case JTokenType.String:
                return token.Value<string>();
                
            case JTokenType.Boolean:
                return token.Value<bool>();
                
            case JTokenType.Null:
                return null;
                
            case JTokenType.Date:
                return token.Value<DateTime>();
                
            default:
                return token.ToString();
        }
    }
}