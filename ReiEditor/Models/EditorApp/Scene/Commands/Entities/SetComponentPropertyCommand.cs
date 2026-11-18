using System;
using System.Collections.Generic;
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
        var before = Normalize(target.Property, target.Property.Value);
        var after = Normalize(target.Property, target.Value);
        if (before == null || after == null || before.Equals(after)) return Task.FromResult<object?>(null);

        Write(target.Property, after);
        return Task.FromResult(Matches(target.Property, after) ? before : null);
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, object?>.CreateHistoryEntry(Target target, object? before)
    {
        var after = Normalize(target.Property, target.Value);
        return before != null && after != null ? new HistoryEntry(this, target, before, after) : null;
    }

    private bool IsCurrent(Target target) =>
        !engine.IsEngineStarting.Value && !engine.IsPlaymodeActive.Value
        && !(engine.IsActive.Value && engine.ActiveMode == EngineRunMode.PlayMode)
        && ReferenceEquals(target.Scene, scenes.CurrentScene.Value)
        && ReferenceEquals(target.Scene.GetById(target.Entity.Id), target.Entity)
        && target.Entity.HasBehaviour(target.Component)
        && target.Property.ParentProperty == null
        && target.Component.Properties.TryGetValue(target.Property.Name, out var property)
        && ReferenceEquals(property, target.Property);

    private static bool Matches(SerializedProperty property, object expected) => Equals(Normalize(property, property.Value), expected);

    private static void Write(SerializedProperty property, object value)
    {
        if (value is SerializedColorValue color) color.Apply(property);
        else if (IsAssetReference(property))
        {
            if (GetAssetIdProperty(property.Value) is { } id) id.Value = value;
        }
        else property.Value = value;
    }

    private static object? Normalize(SerializedProperty property, object? value) => property.Type switch
    {
        SerializedTypeEnum.Custom when property.SourceType == "Color" =>
            value is SerializedColorValue color ? color : SerializedColorValue.Read(value),
        SerializedTypeEnum.Custom when IsAssetReference(property) => value as string ?? GetAssetIdProperty(value)?.Value as string,
        SerializedTypeEnum.String when value is string text => text,
        SerializedTypeEnum.Boolean when value is bool flag => (object)flag,
        SerializedTypeEnum.Integer or SerializedTypeEnum.Enum when value is int integer => (object)integer,
        SerializedTypeEnum.Integer or SerializedTypeEnum.Enum when value is long integer && integer is >= int.MinValue and <= int.MaxValue => (object)(int)integer,
        SerializedTypeEnum.Float when value is float or double or int or long && float.IsFinite(Convert.ToSingle(value)) => Convert.ToSingle(value),
        _ => null
    };

    private static bool IsAssetReference(SerializedProperty property) =>
        property.SourceType.StartsWith("AssetRef<", StringComparison.Ordinal);

    private static SerializedProperty? GetAssetIdProperty(object? value) =>
        value is Dictionary<string, SerializedProperty> children && children.TryGetValue("Id", out var id) ? id : null;
}
