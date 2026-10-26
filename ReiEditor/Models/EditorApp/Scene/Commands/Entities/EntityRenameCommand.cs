using System;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Scenes;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class EntityRenameCommand : IEntityRenameCommand, IUndoableEditorCommand<EntityRenameCommandTarget, EntityRenameCommandResult>
{
    private sealed class RenameHistoryEntry(
        IEntityRenameCommand command,
        ISceneManagementService scenes,
        EditorScene scene,
        GameEntity entity,
        EntityRenameCommandResult result) : IEditorHistoryEntry
    {
        public bool CanUndo => CanRename(_afterName);
        public bool CanRedo => CanRename(_beforeName);
        private readonly string _beforeName = result.BeforeName;
        private readonly string _afterName = result.AfterName;

        private bool CanRename(string expectedName) => ReferenceEquals(scene, scenes.CurrentScene.Value)
            && ReferenceEquals(scene.GetById(entity.Id), entity) && entity.Name == expectedName;

        public Task UndoAsync() => RenameAsync(_afterName, _beforeName);
        public Task RedoAsync() => RenameAsync(_beforeName, _afterName);

        private async Task RenameAsync(string expectedName, string name)
        {
            if (!CanRename(expectedName)) throw new InvalidOperationException("The entity cannot be renamed in the current context.");
            var applied = await command.ExecuteAsync(new EntityRenameCommandTarget(entity, name));
            if (applied.AfterName != name) throw new InvalidOperationException("The history rename was not applied.");
        }
    }

    private readonly ISceneManagementService _scenes;
    private readonly IEntityManagementService _entityManagementService;
    private readonly ILogger<EntityRenameCommand> _logger;

    public EntityRenameCommand(IEntityManagementService entityManagementService, ILogger<EntityRenameCommand> logger, ISceneManagementService scenes)
    {
        _entityManagementService = entityManagementService;
        _logger = logger;
        _scenes = scenes;
    }

    public IEditorHistoryEntry? CreateHistoryEntry(EntityRenameCommandTarget target, EntityRenameCommandResult result)
    {
        if (!result.Changed) return null;
        var scene = _scenes.CurrentScene.Value;
        if (scene == null || !ReferenceEquals(scene.GetById(target.Entity.Id), target.Entity))
            throw new InvalidOperationException("The renamed entity does not belong to the current scene.");
        return new RenameHistoryEntry(this, _scenes, scene, target.Entity, result);
    }

    public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target)
    {
        if (!ReferenceEquals(_scenes.CurrentScene.Value?.GetById(target.Entity.Id), target.Entity))
            throw new InvalidOperationException("The entity does not belong to the current scene.");
        _logger.Log($"Renaming entity {target.Entity.Id}:{target.Entity.Name} -> {target.Name}");
        var beforeName = target.Entity.Name;
        _entityManagementService.RenameEntity(target.Entity, target.Name);
        return Task.FromResult(new EntityRenameCommandResult(beforeName, target.Entity.Name));
    }
}
