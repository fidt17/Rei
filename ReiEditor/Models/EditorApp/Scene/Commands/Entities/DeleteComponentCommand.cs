using System;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed class DeleteComponentCommand(
    IEditorCommandService commands,
    ISceneManagementService scenes,
    IEntityManagementService entities,
    IBehaviourRegistry registry,
    IBehaviourComponentsService components,
    IEngineRunner engine,
    IEntityApi entityApi,
    IEntitySyncService sync) : IUndoableEditorCommand<DeleteComponentCommand.Target, bool>
{
    private sealed class HistoryEntry(DeleteComponentCommand command, Target target) : IEditorHistoryEntry
    {
        public bool CanUndo => command.CanRestore(target);
        public bool CanRedo => command.CanDelete(target);

        public Task UndoAsync()
        {
            if (!CanUndo || !command.Restore(target)) throw new InvalidOperationException("The component was not restored.");
            return Task.CompletedTask;
        }

        public Task RedoAsync()
        {
            if (!CanRedo || !command.Delete(target)) throw new InvalidOperationException("The component was not removed.");
            return Task.CompletedTask;
        }
    }

    public sealed record Target(EditorScene Scene, GameEntity Entity, BehaviourComponent Component);

    public Task DeleteAsync(GameEntity entity, BehaviourComponent component)
    {
        if (commands.IsBusy || scenes.CurrentScene.Value is not { } scene) return Task.CompletedTask;
        return commands.ExecuteAsync(this, new Target(scene, entity, component));
    }

    Task<bool> IEditorCommand<Target, bool>.ExecuteAsync(Target target) => Task.FromResult(CanDelete(target) && Delete(target));

    IEditorHistoryEntry? IUndoableEditorCommand<Target, bool>.CreateHistoryEntry(Target target, bool deleted) =>
        deleted ? new HistoryEntry(this, target) : null;

    private bool Delete(Target target)
    {
        entities.DeleteBehaviour(target.Entity, target.Component.Id);
        return !target.Entity.HasComponent(target.Component.Id);
    }

    private bool Restore(Target target)
    {
        if (engine.IsActive.Value)
        {
            var data = SerializedPropertyJsonConverter.SerializeRuntimeProperties(target.Component.Properties.Values)
                .Properties().ToDictionary(field => field.Name, field => (object?)field.Value);
            data.Add(SetEntityDataRequest.REI_BEHAVIOUR_ID, target.Component.Id);
            var id = target.Entity.Id;
            entityApi.AddBehaviour(id, target.Component.Id);
            var state = entityApi.GetEntityData(id);
            var name = registry.Behaviours[target.Component.Id].ObjectName;
            if (state?.Behaviours.Any(behaviour => Equals(behaviour["REI_TYPE"], name)) != true) return false;
            entityApi.SetData(new SetEntityDataRequest { SceneId = id, Behaviours = [data] });
        }

        components.RestoreComponent(target.Entity, target.Component);
        sync.UpdateEntityState(target.Entity);
        return target.Entity.HasBehaviour(target.Component);
    }

    private bool CanDelete(Target target) => IsCurrent(target) && target.Entity.HasBehaviour(target.Component)
        && !components.TryGetRequiringComponent(target.Entity, target.Component.Id, out _);

    private bool CanRestore(Target target) => IsCurrent(target) && !target.Entity.HasComponent(target.Component.Id)
        && registry.TryGetById(target.Component.Id, out var info)
        && info.RequiredComponentNames.All(name => registry.GetIdByName(name) is { } id && target.Entity.HasComponent(id));

    private bool IsCurrent(Target target) => ReferenceEquals(scenes.CurrentScene.Value, target.Scene)
        && ReferenceEquals(target.Scene.GetById(target.Entity.Id), target.Entity)
        && !engine.IsEngineStarting.Value && !engine.IsPlaymodeActive.Value
        && !(engine.IsActive.Value && engine.ActiveMode == EngineRunMode.PlayMode);
}
