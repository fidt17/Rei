using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed class SetDataAssetPropertyCommand(
    IEditorCommandService commands,
    IDataAssetService assets,
    IAssetRegistry registry,
    IEngineRunner engine)
    : IUndoableEditorCommand<SetDataAssetPropertyCommand.Target, SetDataAssetPropertyCommand.Result?>
{
    private sealed record PropertyKey(string AssetId, string RootName, string? ChildName, SerializedTypeEnum Type, string SourceType);

    private sealed class HistoryEntry(SetDataAssetPropertyCommand command, PropertyKey key, object before, object after) : IEditorHistoryEntry
    {
        private bool _applied = true;

        public bool IsStale => command.CanRecord && FindMatchingProperty(_applied ? after : before) == null;
        public bool CanUndo => _applied && command.CanRecord;
        public bool CanRedo => !_applied && command.CanRecord;

        public Task UndoAsync() => Replay(undo: true, after, before);
        public Task RedoAsync() => Replay(undo: false, before, after);

        private (DataAsset Asset, SerializedProperty Property)? FindMatchingProperty(object expected)
        {
            var target = command.FindProperty(key);
            return target is { } current && ReadValue(current.Property) is { } value && ValuesEqual(key.Type, value, expected)
                ? target : null;
        }

        private Task Replay(bool undo, object expected, object value)
        {
            if (_applied != undo || !command.CanRecord || FindMatchingProperty(expected) is not { } target
                || command.Apply(target.Asset, target.Property, value) == null)
                throw new InvalidOperationException("The DataAsset property could not be restored.");
            _applied = !undo;
            return Task.CompletedTask;
        }
    }

    public sealed record Target(DataAsset Asset, SerializedProperty Property, object? Value);
    public sealed record Result(object Before, object After, bool RuntimeSynced);

    private SerializedProperty? _applyingProperty;
    private bool CanRecord => !engine.IsPlaymodeActive.Value && !engine.IsEngineStarting.Value;

    internal static bool Supports(SerializedProperty property)
    {
        if (property.ParentProperty is { } parent)
            return SupportsNestedFields(parent) && property.Type.SupportsNestedFieldHistory();

        return property.Type switch
        {
            SerializedTypeEnum.Integer or SerializedTypeEnum.Float or SerializedTypeEnum.Boolean
                or SerializedTypeEnum.Enum or SerializedTypeEnum.String => true,
            SerializedTypeEnum.Collection => property.ItemType.SupportsCollectionItemHistory(CollectionItemSourceType(property)),
            SerializedTypeEnum.Custom => SupportsCustomValue(property.SourceType),
            _ => false
        };
    }

    internal static bool SupportsNestedFields(SerializedProperty property) => property.ParentProperty == null
        && property.Type == SerializedTypeEnum.Custom && !SupportsCustomValue(property.SourceType) && !property.SourceType.IsComponentReference();

    private static bool SupportsCustomValue(string sourceType) =>
        sourceType is "Vector2" or "Vector3" or "Color" || sourceType.IsAssetReference();

    private static string CollectionItemSourceType(SerializedProperty property) => property.ItemSourceType ?? property.TemplateTypeName ?? "";

    internal static bool IsValidCollectionUpdate(SerializedProperty property, JArray values)
    {
        if (property.ItemType != SerializedTypeEnum.Custom)
            return values.All(value => value is JValue item && property.ItemType.IsValidValue(item.Value));

        var (name, type) = ReferenceField(CollectionItemSourceType(property));
        return values.All(value => value is JObject { Count: 1 } item && item[name]?.Type == type);
    }

    internal bool IsApplying(SerializedProperty property) => ReferenceEquals(_applyingProperty, property);

    public Task<Result?> SetAsync(DataAsset asset, SerializedProperty property, object? value)
    {
        if (commands.IsBusy) return Task.FromResult<Result?>(null);
        if (property.ParentProperty is { Type: SerializedTypeEnum.Collection } collection)
        {
            if (!Supports(collection) || EditCollectionItem(collection, property, value) is not { } update)
                return Task.FromResult<Result?>(null);
            property = collection;
            value = update;
        }

        var target = new Target(asset, property, value);
        return CanRecord ? commands.ExecuteAsync(this, target) : Task.FromResult(ApplyEdit(target));
    }

    internal void RecordApplied(DataAsset asset, SerializedProperty property, object before)
    {
        if (!CanRecord || commands.IsBusy || !IsCurrent(asset, property)
            || ReadValue(property) is not { } after || ValuesEqual(property.Type, before, after)) return;
        commands.RecordApplied(new HistoryEntry(this, GetKey(asset, property), before, after));
    }

    Task<Result?> IEditorCommand<Target, Result?>.ExecuteAsync(Target target) => Task.FromResult(ApplyEdit(target));

    IEditorHistoryEntry? IUndoableEditorCommand<Target, Result?>.CreateHistoryEntry(Target target, Result? result) =>
        result != null && !ValuesEqual(target.Property.Type, result.Before, result.After)
            ? new HistoryEntry(this, GetKey(target.Asset, target.Property), result.Before, result.After)
            : null;

    private Result? ApplyEdit(Target target)
    {
        if (!IsCurrent(target.Asset, target.Property) || !target.Property.Type.IsValidValue(target.Value)) return null;
        return Apply(target.Asset, target.Property, target.Value!);
    }

    private bool IsCurrent(DataAsset asset, SerializedProperty property)
    {
        var current = FindProperty(GetKey(asset, property));
        return ReferenceEquals(current?.Asset, asset) && ReferenceEquals(current?.Property, property);
    }

    private Result? Apply(DataAsset asset, SerializedProperty property, object value)
    {
        if (ReadValue(property) is not { } before) return null;
        var update = property.Type switch
        {
            SerializedTypeEnum.Custom => CreateCustomUpdate(property, before, value),
            SerializedTypeEnum.Collection when value is IReadOnlyList<SerializedProperty> items => SnapshotCollection(items),
            SerializedTypeEnum.Collection => value as JArray,
            _ => value
        };
        if (update == null || update is JArray list && !IsValidCollectionUpdate(property, list)) return null;

        var root = property.ParentProperty ?? property;
        var rootUpdate = ReferenceEquals(root, property) ? update : new JObject { [property.Name] = new JValue(update) };
        _applyingProperty = root;
        try
        {
            if (!assets.TrySetProperty(asset, root.Name, rootUpdate, out var runtimeSynced) ||
                ReadValue(property) is not { } after) return null;
            if (before is JArray previous && after is JArray current && previous.Count == current.Count
                && property.Value is List<SerializedProperty> currentItems)
            {
                // Resize rebuilds the rows. When the list keeps its shape, update changed scalar rows in place.
                for (var index = 0; index < currentItems.Count; index++)
                {
                    if (currentItems[index].Type != SerializedTypeEnum.Custom && !JToken.DeepEquals(previous[index], current[index]))
                        currentItems[index].TriggerChangedEvent();
                }
            }
            return MatchesUpdate(property, after, update) ? new Result(before, after, runtimeSynced) : null;
        }
        finally { _applyingProperty = null; }
    }

    private (DataAsset Asset, SerializedProperty Property)? FindProperty(PropertyKey key)
    {
        if (!registry.TryGetLoadedAsset(key.AssetId, out var loaded) || loaded is not DataAsset asset
            || !asset.Properties.TryGetValue(key.RootName, out var root)) return null;
        var property = key.ChildName is { } childName
            ? (root.Value as IReadOnlyDictionary<string, SerializedProperty>)?.GetValueOrDefault(childName)
            : root;
        if (property == null || !Supports(property) || property.Type != key.Type || property.SourceType != key.SourceType) return null;
        return (asset, property);
    }

    private static PropertyKey GetKey(DataAsset asset, SerializedProperty property) =>
        property.ParentProperty is { } parent
            ? new(asset.AssetId, parent.Name, property.Name, property.Type, property.SourceType)
            : new(asset.AssetId, property.Name, null, property.Type, property.SourceType);

    private static object? ReadValue(SerializedProperty property)
    {
        if (property.Type == SerializedTypeEnum.Collection)
            return property.Value is IReadOnlyList<SerializedProperty> items ? SnapshotCollection(items) : null;
        if (property.Type != SerializedTypeEnum.Custom) return property.Value;
        if (property.SourceType == "Color") return SerializedColorValue.Read(property.Value);
        if (property.SourceType.IsAssetReference())
            return (property.Value as IReadOnlyDictionary<string, SerializedProperty>)?.GetValueOrDefault("Id")?.Value as string;
        return ReadFields(property);
    }

    private static JArray SnapshotCollection(IEnumerable<SerializedProperty> items) =>
        new(items.Select(item => item.Type == SerializedTypeEnum.Custom ? (JToken?)ReadFields(item) : new JValue(item.Value)));

    private static (string Name, JTokenType Type) ReferenceField(string sourceType) =>
        sourceType.IsAssetReference() ? ("Id", JTokenType.String) : ("SceneEntityId", JTokenType.Integer);

    private static JArray? EditCollectionItem(SerializedProperty collection, SerializedProperty item, object? value)
    {
        if (collection.Value is not List<SerializedProperty> items || !item.Type.SupportsCollectionItemHistory(item.SourceType)
            || !item.Type.IsValidValue(value)) return null;
        var index = items.IndexOf(item);
        if (index < 0) return null;

        var update = SnapshotCollection(items);
        var token = new JValue(value);
        if (item.Type == SerializedTypeEnum.Custom)
        {
            var (name, type) = ReferenceField(item.SourceType);
            if (update[index] is not JObject reference || token.Type != type) return null;
            reference[name] = token;
        }
        else update[index] = token;
        return update;
    }

    private static JObject? CreateCustomUpdate(SerializedProperty property, object before, object value)
    {
        if (!property.SourceType.IsAssetReference()) return CreateNumericFieldPatch(before as JObject ?? ReadFields(property), value);

        var id = value switch
        {
            string selectedId => selectedId,
            JObject patch when patch["Id"] is JValue { Value: string selectedId } => selectedId,
            _ => null
        };
        return id != null ? new JObject { ["Id"] = id } : null;
    }

    private static JObject? ReadFields(SerializedProperty property) =>
        property.Value is IReadOnlyDictionary<string, SerializedProperty> fields
            ? new JObject(fields.Select(field => new JProperty(field.Key, field.Value.Value)))
            : null;

    private static JObject? CreateNumericFieldPatch(JObject? before, object value)
    {
        var requested = value switch
        {
            Vector2 vector => new JObject { ["x"] = vector.X, ["y"] = vector.Y },
            Vector3 vector => new JObject { ["x"] = vector.X, ["y"] = vector.Y, ["z"] = vector.Z },
            SerializedColorValue color => new JObject { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B, ["a"] = color.A },
            JObject patch => patch,
            _ => null
        };
        if (before == null || requested == null) return null;

        var changes = new JObject();
        foreach (var field in requested.Properties())
        {
            if (before[field.Name] is not JValue previous || field.Value is not JValue next
                || previous.Type is not (JTokenType.Integer or JTokenType.Float)
                || next.Type is not (JTokenType.Integer or JTokenType.Float) || !double.IsFinite(next.Value<double>())) return null;
            // The Inspector reads these fields as floats. Preserve the serialized value of untouched fields.
            var same = value is JObject
                ? previous.Value<double>() == next.Value<double>()
                : previous.Value<float>() == next.Value<float>();
            if (!same) changes.Add(field.Name, next.DeepClone());
        }
        return changes;
    }

    private static bool MatchesUpdate(SerializedProperty property, object value, object update)
    {
        if (update is JObject patch)
        {
            if (value is string id) return id == patch["Id"]?.Value<string>();
            var fields = value as JObject ?? ReadFields(property);
            return fields != null && patch.Properties().All(field => fields[field.Name]?.Value<double>() == field.Value.Value<double>());
        }
        return ValuesEqual(property.Type, value, update);
    }

    private static bool ValuesEqual(SerializedTypeEnum type, object left, object right) => type switch
    {
        SerializedTypeEnum.Integer or SerializedTypeEnum.Enum => Convert.ToDecimal(left) == Convert.ToDecimal(right),
        SerializedTypeEnum.Float => Convert.ToDouble(left).Equals(Convert.ToDouble(right)),
        SerializedTypeEnum.Custom or SerializedTypeEnum.Collection when left is JToken previous && right is JToken next =>
            JToken.DeepEquals(previous, next),
        _ => Equals(left, right)
    };
}
