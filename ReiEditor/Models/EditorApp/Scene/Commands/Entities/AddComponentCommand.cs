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
    IEngineRunner engine) : IUndoableEditorCommand<AddComponentCommand.Target, BehaviourComponent[]?>
{
    private sealed class HistoryEntry(AddComponentCommand command, Target target, BehaviourComponent[] added) : IEditorHistoryEntry
    {
        public bool CanUndo => command.CanRemove(target, added);
        public bool CanRedo => command.CanRestore(target, added);

        public Task UndoAsync()
        {
            if (!CanUndo) throw new InvalidOperationException("The added component cannot be removed.");
            for (var i = added.Length - 1; i >= 0; i--) command.Remove(target.Entity, added[i].Id);
            return Task.CompletedTask;
        }

        public Task RedoAsync()
        {
            if (!CanRedo) throw new InvalidOperationException("The component cannot be restored.");
            for (var i = 0; i < added.Length; i++)
            {
                var restored = command.Add(target.Entity, added[i].Id) ?? throw new InvalidOperationException("The component was not restored.");
                restored.HistoryIdentity = added[i].HistoryIdentity;
                added[i] = restored;
            }
            return Task.CompletedTask;
        }
    }

    public sealed record Target(EditorScene Scene, GameEntity Entity, int ComponentId);

    public Task AddAsync(GameEntity entity, int componentId)
    {
        if (commands.IsBusy || scenes.CurrentScene.Value is not { } scene) return Task.CompletedTask;
        return commands.ExecuteAsync(this, new Target(scene, entity, componentId));
    }

    Task<BehaviourComponent[]?> IEditorCommand<Target, BehaviourComponent[]?>.ExecuteAsync(Target target)
    {
        if (!IsCurrent(target) || target.Entity.HasComponent(target.ComponentId)) return Task.FromResult<BehaviourComponent[]?>(null);
        var before = target.Entity.Behaviours.Select(component => component.Id).ToHashSet();
        if (Add(target.Entity, target.ComponentId) == null) return Task.FromResult<BehaviourComponent[]?>(null);
        // Editor sync inserts required components before the components that use them.
        return Task.FromResult<BehaviourComponent[]?>(target.Entity.Behaviours.Where(component => !before.Contains(component.Id)).ToArray());
    }

    IEditorHistoryEntry? IUndoableEditorCommand<Target, BehaviourComponent[]?>.CreateHistoryEntry(Target target, BehaviourComponent[]? added) =>
        added is { Length: > 0 } ? new HistoryEntry(this, target, added) : null;

    private BehaviourComponent? Add(GameEntity entity, int componentId)
    {
        entities.AddBehaviour(entity, componentId);
        return entity.GetBehaviour(componentId);
    }

    private void Remove(GameEntity entity, int componentId)
    {
        entities.DeleteBehaviour(entity, componentId);
        if (entity.HasComponent(componentId)) throw new InvalidOperationException("The component was not removed.");
    }

    private bool CanRemove(Target target, BehaviourComponent[] added)
    {
        if (!IsCurrent(target) || !added.All(target.Entity.HasBehaviour)) return false;
        var addedIds = added.Select(component => component.Id).ToHashSet();
        return !target.Entity.Behaviours.Any(component => !addedIds.Contains(component.Id)
            && registry.TryGetById(component.Id, out var info)
            && info.RequiredComponentNames.Any(name => registry.GetIdByName(name) is { } id && addedIds.Contains(id)));
    }

    private bool CanRestore(Target target, BehaviourComponent[] added)
    {
        if (!IsCurrent(target)) return false;
        var available = target.Entity.Behaviours.Select(component => component.Id).ToHashSet();
        foreach (var component in added)
        {
            if (!registry.TryGetById(component.Id, out var info)
                || !info.RequiredComponentNames.All(name => registry.GetIdByName(name) is { } id && available.Contains(id))
                || !available.Add(component.Id)) return false;
        }
        return true;
    }

    private bool IsCurrent(Target target) => ReferenceEquals(scenes.CurrentScene.Value, target.Scene)
        && EntityEditContext.CanEdit(engine, target.Scene, target.Entity);
}
