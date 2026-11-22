using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed class SetComponentPropertyCommand(
    IEditorCommandService commands,
    ISceneManagementService scenes,
    IEngineRunner engine) : IUndoableEditorCommand<SetComponentPropertyCommand.Target, object?>
{
    private sealed class HistoryEntry(SetComponentPropertyCommand command, Target target, object before, object after) : IEditorHistoryEntry
    {
        public bool CanUndo => command.IsCurrent(target) && Matches(target.Property, after);
        public bool CanRedo => command.IsCurrent(target) && Matches(target.Property, before);

        public Task UndoAsync() => Apply(CanUndo, before);
        public Task RedoAsync() => Apply(CanRedo, after);

        private Task Apply(bool allowed, object value)
        {
            if (!allowed) throw new InvalidOperationException("The component property has changed or is unavailable.");
            Write(target.Property, value);
            if (!Matches(target.Property, value)) throw new InvalidOperationException("The component property could not be updated.");
            return Task.CompletedTask;
        }
    }

    public sealed record Target(EditorScene Scene, GameEntity Entity, BehaviourComponent Component, SerializedProperty Property, object Value);

    public Task SetAsync(GameEntity entity, BehaviourComponent component, SerializedProperty property, object value)
    {
        if (commands.IsBusy || scenes.CurrentScene.Value is not { } scene) return Task.CompletedTask;
        return commands.ExecuteAsync(this, new Target(scene, entity, component, property, value));
    }

    public void RecordAppliedColor(GameEntity entity, BehaviourComponent component, SerializedProperty property, SerializedColorValue before)
    {
        if (commands.IsBusy || scenes.CurrentScene.Value is not { } scene) return;
        if (property.SourceType != "Color" || SerializedColorValue.Read(property.Value) is not { } after || before == after) return;

        var target = new Target(scene, entity, component, property, after);
        if (IsCurrent(target)) commands.RecordApplied(new HistoryEntry(this, target, before, after));
    }

    Task<object?> IEditorCommand<Target, object?>.ExecuteAsync(Target target)
    {
        if (!IsCurrent(target)) return Task.FromResult<object?>(null);
        var before = Read(target.Property);
        var after = Normalize(GetValueProperty(target.Property), target.Value);
        if (before == null || after == null || SameValue(before, after)) return Task.FromResult<object?>(null);

        Write(target.Property, after);
        return Task.FromResult(Matches(target.Property, after) ? before : null);
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, object?>.CreateHistoryEntry(Target target, object? before)
    {
        var after = Normalize(GetValueProperty(target.Property), target.Value);
        return before != null && after != null ? new HistoryEntry(this, target, before, after) : null;
    }

    private bool IsCurrent(Target target)
    {
        var root = target.Property.ParentProperty ?? target.Property;
        return !engine.IsEngineStarting.Value && !engine.IsPlaymodeActive.Value
            && !(engine.IsActive.Value && engine.ActiveMode == EngineRunMode.PlayMode)
            && ReferenceEquals(target.Scene, scenes.CurrentScene.Value)
            && ReferenceEquals(target.Scene.GetById(target.Entity.Id), target.Entity)
            && target.Entity.HasBehaviour(target.Component)
            && target.Component.Properties.TryGetValue(root.Name, out var property)
            && ReferenceEquals(property, root)
            && (ReferenceEquals(root, target.Property) || IsCurrentCollectionItem(root, target.Property));
    }

    private static bool IsCurrentCollectionItem(SerializedProperty collection, SerializedProperty item) =>
        collection.Type == SerializedTypeEnum.Collection && item.Type.SupportsCollectionItemHistory(item.SourceType)
        && collection.Value is List<SerializedProperty> items && items.Contains(item);

    private static bool Matches(SerializedProperty property, object expected) => SameValue(Read(property), expected);

    private static bool SameValue(object? left, object? right) =>
        left is SerializedProperty[] items && right is SerializedProperty[] other ? items.SequenceEqual(other) : Equals(left, right);

    private static object? Read(SerializedProperty property)
    {
        var valueProperty = GetValueProperty(property);
        return Normalize(valueProperty, valueProperty.Value);
    }

    private static void Write(SerializedProperty property, object value)
    {
        if (value is SerializedColorValue color) color.Apply(property);
        else if (value is Vector2 vector && property.Value is Dictionary<string, SerializedProperty> coordinates && Read(property) is Vector2 previous)
        {
            if (previous.X != vector.X) coordinates["x"].Value = vector.X;
            if (previous.Y != vector.Y) coordinates["y"].Value = vector.Y;
        }
        else if (value is Vector3 vector3 && property.Value is Dictionary<string, SerializedProperty> coordinates3 && Read(property) is Vector3 previous3)
        {
            if (previous3.X != vector3.X) coordinates3["x"].Value = vector3.X;
            if (previous3.Y != vector3.Y) coordinates3["y"].Value = vector3.Y;
            if (previous3.Z != vector3.Z) coordinates3["z"].Value = vector3.Z;
        }
        else if (value is SerializedProperty[] items)
        {
            SerializedProperty.ReindexCollectionItems(items);
            property.Value = new List<SerializedProperty>(items);
        }
        else GetValueProperty(property).Value = value;
    }

    private static object? Normalize(SerializedProperty property, object? value) => property.Type switch
    {
        SerializedTypeEnum.Custom when property.SourceType == "Color" =>
            value is SerializedColorValue color ? color : SerializedColorValue.Read(value),
        SerializedTypeEnum.Custom when property.SourceType == "Vector2" => value switch
        {
            Vector2 vector when float.IsFinite(vector.X) && float.IsFinite(vector.Y) => vector,
            Dictionary<string, SerializedProperty> coordinates when ReadCoordinate(coordinates, "x") is { } x
                && ReadCoordinate(coordinates, "y") is { } y => new Vector2(x, y),
            _ => null
        },
        SerializedTypeEnum.Custom when property.SourceType == "Vector3" => value switch
        {
            Vector3 vector when float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z) => vector,
            Dictionary<string, SerializedProperty> coordinates when ReadCoordinate(coordinates, "x") is { } x
                && ReadCoordinate(coordinates, "y") is { } y && ReadCoordinate(coordinates, "z") is { } z => new Vector3(x, y, z),
            _ => null
        },
        SerializedTypeEnum.String when value is string text => text,
        SerializedTypeEnum.Collection when value is IReadOnlyList<SerializedProperty> items => items.ToArray(),
        SerializedTypeEnum.Boolean when value is bool flag => (object)flag,
        SerializedTypeEnum.Integer or SerializedTypeEnum.Enum when value is int integer => (object)integer,
        SerializedTypeEnum.Integer or SerializedTypeEnum.Enum when value is long integer && integer is >= int.MinValue and <= int.MaxValue => (object)(int)integer,
        SerializedTypeEnum.Float when value is float or double or int or long && float.IsFinite(Convert.ToSingle(value)) => Convert.ToSingle(value),
        _ => null
    };

    private static float? ReadCoordinate(Dictionary<string, SerializedProperty> coordinates, string name) =>
        coordinates.TryGetValue(name, out var coordinate) && Normalize(coordinate, coordinate.Value) is float value ? value : null;

    private static SerializedProperty GetValueProperty(SerializedProperty property)
    {
        if (property.Type != SerializedTypeEnum.Custom || property.Value is not Dictionary<string, SerializedProperty> children) return property;
        if (property.SourceType.IsAssetReference()) return children.GetValueOrDefault("Id") ?? property;
        if (property.SourceType.IsComponentReference()) return children.GetValueOrDefault("SceneEntityId") ?? property;
        return property;
    }
}
