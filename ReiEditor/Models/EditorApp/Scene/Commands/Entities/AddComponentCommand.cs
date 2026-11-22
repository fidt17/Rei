using System;
using System.Linq;
using System.Threading.Tasks;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed class AddComponentCommand(
    IEditorCommandService commands,
    ISceneManagementService scenes,
    IEntityManagementService entities,
    IBehaviourRegistry registry,
    IBehaviourComponentsService components,
    IEngineRunner engine) : IUndoableEditorCommand<AddComponentCommand.Target, BehaviourComponent?>
{
    private sealed class HistoryEntry(AddComponentCommand command, Target target, BehaviourComponent component) : IEditorHistoryEntry
    {
        private BehaviourComponent _component = component;

        public bool CanUndo => command.CanRemove(target, _component);
        public bool CanRedo => command.IsCurrent(target) && !target.Entity.HasComponent(target.ComponentId) && command.HasRequirements(target);

        public Task UndoAsync()
        {
            if (!CanUndo) throw new InvalidOperationException("The added component cannot be removed.");
            command.Remove(target);
            if (target.Entity.HasComponent(target.ComponentId)) throw new InvalidOperationException("The component was not removed.");
            return Task.CompletedTask;
        }

        public Task RedoAsync()
        {
            if (!CanRedo) throw new InvalidOperationException("The component cannot be restored.");
            var restored = command.Add(target) ?? throw new InvalidOperationException("The component was not restored.");
            restored.HistoryIdentity = _component.HistoryIdentity;
            _component = restored;
            return Task.CompletedTask;
        }
    }

    public sealed record Target(EditorScene Scene, GameEntity Entity, int ComponentId);

    public Task AddAsync(GameEntity entity, int componentId)
    {
        if (commands.IsBusy || scenes.CurrentScene.Value is not { } scene) return Task.CompletedTask;
        return commands.ExecuteAsync(this, new Target(scene, entity, componentId));
    }

    Task<BehaviourComponent?> IEditorCommand<Target, BehaviourComponent?>.ExecuteAsync(Target target)
    {
        if (!IsCurrent(target) || target.Entity.HasComponent(target.ComponentId)) return Task.FromResult<BehaviourComponent?>(null);
        var before = target.Entity.Behaviours.Count();
        var component = Add(target);
        return Task.FromResult(target.Entity.Behaviours.Count() == before + 1 ? component : null);
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, BehaviourComponent?>.CreateHistoryEntry(Target target, BehaviourComponent? component) =>
        component == null ? null : new HistoryEntry(this, target, component);

    private BehaviourComponent? Add(Target target)
    {
        entities.AddBehaviour(target.Entity, target.ComponentId);
        return target.Entity.GetBehaviour(target.ComponentId);
    }

    private void Remove(Target target) => entities.DeleteBehaviour(target.Entity, target.ComponentId);

    private bool CanRemove(Target target, BehaviourComponent component) => IsCurrent(target) && target.Entity.HasBehaviour(component)
        && !components.TryGetRequiringComponent(target.Entity, target.ComponentId, out _);

    private bool HasRequirements(Target target) => registry.TryGetById(target.ComponentId, out var info)
        && info.RequiredComponentNames.All(name => registry.GetIdByName(name) is { } id && target.Entity.HasComponent(id));

    private bool IsCurrent(Target target) => ReferenceEquals(scenes.CurrentScene.Value, target.Scene)
        && ReferenceEquals(target.Scene.GetById(target.Entity.Id), target.Entity)
        && !engine.IsEngineStarting.Value && !engine.IsPlaymodeActive.Value
        && !(engine.IsActive.Value && engine.ActiveMode == EngineRunMode.PlayMode);
}
