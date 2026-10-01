using System;
using System.Collections.Generic;
using System.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public static class PropertyDisplayUtils
{
    public static IEnumerable<SerializedProperty> GetVisibleProperties(IEnumerable<SerializedProperty> properties, IReadOnlyDictionary<string, SerializableObjectInfo.SerializedPropertyData>? schema)
    {
        var values = properties.ToDictionary(property => property.Name);
        if (schema == null) return values.Values;

        var declared = schema.OrderBy(entry => entry.Value.DeclarationIndex)
            .Where(entry => !entry.Value.HideInEditor && values.ContainsKey(entry.Key))
            .Select(entry => values[entry.Key]);
        // Preserve schema-less fields, as previous DataAsset/custom drawers did.
        return declared.Concat(values.Values.Where(property => !schema.ContainsKey(property.Name)));
    }

    public static IEnumerable<BaseViewModel> CreateRows(IEnumerable<SerializedProperty> visibleProperties, IReadOnlyDictionary<string, SerializableObjectInfo.SerializedPropertyData>? schema, Func<SerializedProperty, BaseViewModel> createProperty)
    {
        foreach (var property in visibleProperties)
        {
            if (schema != null && schema.TryGetValue(property.Name, out var metadata) && metadata.HeaderBefore != null)
                yield return new PropertyHeaderViewModel(metadata.HeaderBefore);
            yield return createProperty(property);
        }
    }
}
