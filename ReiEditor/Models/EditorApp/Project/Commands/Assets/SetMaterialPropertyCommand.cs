using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.EditorApp.Project.Commands.Assets;

public sealed class SetMaterialPropertyCommand(
    IEditorCommandService commands,
    IAssetRegistry assets,
    IAssetRuntimeSyncService runtime,
    IEngineRunner engine)
    : IUndoableEditorCommand<SetMaterialPropertyCommand.Target, SetMaterialPropertyCommand.Result?>
{
    private sealed record PropertyKey(string AssetId, Property Kind, string? UniformName = null, string? ShaderId = null);

    private sealed class HistoryEntry(SetMaterialPropertyCommand command, PropertyKey key, JToken? before, JToken? after) : IEditorHistoryEntry
    {
        private bool _applied = true;

        public bool IsStale => command.CanRecord && FindMatchingMaterial(_applied ? after : before) == null;
        public bool CanUndo => _applied && command.CanRecord;
        public bool CanRedo => !_applied && command.CanRecord;

        public Task UndoAsync() => Replay(after, before, undo: true);
        public Task RedoAsync() => Replay(before, after, undo: false);

        private Material? FindMatchingMaterial(JToken? expected)
        {
            var material = command.FindMaterial(key);
            return material != null && JToken.DeepEquals(Read(material, key), expected) ? material : null;
        }

        private Task Replay(JToken? expected, JToken? value, bool undo)
        {
            if (_applied != undo || !command.CanRecord || FindMatchingMaterial(expected) is not { } material)
                throw new InvalidOperationException("The material property could not be restored.");

            command.Write(material, key, value);
            _applied = !undo;
            return Task.CompletedTask;
        }
    }

    public enum Property
    {
        Uniform,
        UseDepth,
        SortingOrder,
        Shader
    }

    public sealed record Target(Material Material, Property Kind, object Value, string? UniformName = null);
    public sealed record Result(JToken? Before, JToken After, bool RuntimeSynced)
    {
        public bool Changed => !JToken.DeepEquals(Before, After);
    }

    internal event Action<Material, Property, string?, string?>? PropertyChanged;
    private bool CanRecord => !engine.IsPlaymodeActive.Value && !engine.IsEngineStarting.Value;

    public Task<Result?> SetUniformAsync(Material material, string name, object value) =>
        SetAsync(new Target(material, Property.Uniform, value, name));

    internal Task<Result?> SetUseDepthAsync(Material material, bool value) =>
        SetAsync(new Target(material, Property.UseDepth, value));

    internal Task<Result?> SetSortingOrderAsync(Material material, int value) =>
        SetAsync(new Target(material, Property.SortingOrder, value));

    internal Task<Result?> SetShaderAsync(Material material, string id, IReadOnlyDictionary<string, object?> properties) =>
        SetAsync(new Target(material, Property.Shader, ShaderState(id, properties)));

    private Task<Result?> SetAsync(Target target)
    {
        if (commands.IsBusy) return Task.FromResult<Result?>(null);
        return CanRecord ? commands.ExecuteAsync(this, target) : Task.FromResult(Apply(target));
    }

    internal void RecordApplied(Material material, string name, JToken? before)
    {
        var key = new PropertyKey(material.AssetId, Property.Uniform, name, material.ShaderAssetId);
        if (!CanRecord || commands.IsBusy || !ReferenceEquals(material, FindMaterial(key))) return;
        var current = ReadUniform(material, name);
        if (!JToken.DeepEquals(before, current)) commands.RecordApplied(new HistoryEntry(this, key, before, current));
    }

    Task<Result?> IEditorCommand<Target, Result?>.ExecuteAsync(Target target) => Task.FromResult(Apply(target));

    IEditorHistoryEntry? IUndoableEditorCommand<Target, Result?>.CreateHistoryEntry(Target target, Result? result) =>
        result is { Changed: true }
            ? new HistoryEntry(this, GetKey(target), result.Before, result.After)
            : null;

    private Result? Apply(Target target)
    {
        var material = target.Material;
        var key = GetKey(target);
        if (!ReferenceEquals(material, FindMaterial(key))) return null;
        var before = Read(material, key);
        var after = JToken.FromObject(target.Value);
        return new Result(before, after, JToken.DeepEquals(before, after) || Write(material, key, after));
    }

    private bool Write(Material material, PropertyKey key, JToken? value)
    {
        switch (key.Kind)
        {
            case Property.Uniform:
                if (value == null) material.Properties.Remove(key.UniformName!);
                else material.Properties[key.UniformName!] = value.ToObject<object>();
                break;
            case Property.UseDepth:
                material.SetUseDepth(value!.Value<bool>());
                break;
            case Property.SortingOrder:
                material.SetSortingOrder(value!.Value<int>());
                break;
            case Property.Shader:
                material.SetShader(value![nameof(Material.ShaderAssetId)]!.Value<string>()!,
                    value[nameof(Material.Properties)]!.ToObject<Dictionary<string, object?>>()!);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(key));
        }

        var json = JsonConvert.SerializeObject(material);
        var synced = runtime.TrySetAssetData(material.AssetId, json);
        PropertyChanged?.Invoke(material, key.Kind, key.UniformName, synced ? json : null);
        return synced;
    }

    private Material? FindMaterial(PropertyKey key) =>
        assets.TryGetLoadedAsset(key.AssetId, out var asset) && asset is Material material
            && (key.ShaderId == null || material.ShaderAssetId == key.ShaderId)
            ? material
            : null;

    private static PropertyKey GetKey(Target target) =>
        new(target.Material.AssetId, target.Kind, target.UniformName, target.Kind == Property.Uniform ? target.Material.ShaderAssetId : null);

    private static JToken? Read(Material material, PropertyKey key) => key.Kind switch
    {
        Property.Uniform => ReadUniform(material, key.UniformName!),
        Property.UseDepth => new JValue(material.UseDepth),
        Property.SortingOrder => new JValue(material.SortingOrder),
        Property.Shader => ShaderState(material.ShaderAssetId, material.Properties),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static JObject ShaderState(string id, IReadOnlyDictionary<string, object?> properties) =>
        new() { [nameof(Material.ShaderAssetId)] = id, [nameof(Material.Properties)] = JObject.FromObject(properties) };

    internal static JToken? ReadUniform(Material material, string name) => material.Properties.TryGetValue(name, out var value)
        ? value == null ? JValue.CreateNull() : JToken.FromObject(value)
        : null;
}
