using System.Collections.Generic;
using System.Numerics;
using ReiEditor.Models.Services.Components;

namespace ReiEditor.Utils.SerializedProperties;

public static class SerializedPropertyVector3Utility
{
    public static bool TryGetVector3Property(BehaviourComponent behaviour, string propertyName, out Vector3 value)
    {
        value = Vector3.Zero;

        if (!behaviour.HasProperty(propertyName)) return false;
        if (behaviour.GetProperty(propertyName).Value is not Dictionary<string, SerializedProperty> nestedProperties) return false;
        if (!TryGetFloatProperty(nestedProperties, "x", out var x)) return false;
        if (!TryGetFloatProperty(nestedProperties, "y", out var y)) return false;
        if (!TryGetFloatProperty(nestedProperties, "z", out var z)) return false;

        value = new Vector3(x, y, z);
        return true;
    }

    public static IReadOnlyDictionary<string, object?> CreateVector3Value(Vector3 value)
    {
        return new Dictionary<string, object?>
        {
            { "x", value.X },
            { "y", value.Y },
            { "z", value.Z }
        };
    }

    private static bool TryGetFloatProperty(IReadOnlyDictionary<string, SerializedProperty> nestedProperties, string propertyName, out float value)
    {
        value = 0;
        if (!nestedProperties.TryGetValue(propertyName, out var property) || property.Value == null) return false;

        switch (property.Value)
        {
            case float floatValue:
                value = floatValue;
                return true;
            case double doubleValue:
                value = (float)doubleValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            default:
                return false;
        }
    }
}
