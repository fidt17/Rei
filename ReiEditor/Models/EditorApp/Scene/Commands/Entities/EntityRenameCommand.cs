using System;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Engine.Playmode;
using EditorScene = ReiEditor.Models.Services.Scenes.Scene;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using System.Threading.Tasks;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class EntityRenameCommand : IEntityRenameCommand, IUndoableEditorCommand<EntityRenameCommandTarget, EntityRenameCommandResult>
{
    private sealed class RenameHistoryEntry(
        EntityRenameCommand command,
        ISceneManagementService scenes,
        EditorScene scene,
        GameEntity entity,
        EntityRenameCommandResult result) : IEditorHistoryEntry
    {
        public bool CanUndo => FindTarget(_afterName) != null;
        public bool CanRedo => FindTarget(_beforeName) != null;
        private readonly string _beforeName = result.BeforeName;
        private readonly string _afterName = result.AfterName;

        private GameEntity? FindTarget(string expectedName)
        {
            var currentScene = scenes.CurrentScene.Value;
            var target = entity;
            if (!ReferenceEquals(scene, currentScene))
            {
                if (currentScene == null || string.IsNullOrEmpty(scene.AssetId) || currentScene.AssetId != scene.AssetId) return null;
                if (!ReferenceEquals(scene.GetById(entity.Id), entity)) return null;
                var reloaded = currentScene.GetById(entity.Id);
                if (reloaded == null) return null;
                target = reloaded;
            }
            return command.CanExecute(target) && target.Name == expectedName ? target : null;
        }

        public Task UndoAsync() => RenameAsync(_afterName, _beforeName);
        public Task RedoAsync() => RenameAsync(_beforeName, _afterName);

        private async Task RenameAsync(string expectedName, string name)
        {
            var target = FindTarget(expectedName) ?? throw new InvalidOperationException("The entity cannot be renamed in the current context.");
            var applied = await command.ExecuteAsync(new EntityRenameCommandTarget(target, name));
            if (applied.AfterName != name) throw new InvalidOperationException("The history rename was not applied.");
        }
    }

    private readonly ISceneManagementService _scenes;
    private readonly IEngineRunner _engine;
    private readonly IEntityManagementService _entityManagementService;
    private readonly ILogger<EntityRenameCommand> _logger;

    public EntityRenameCommand(IEntityManagementService entityManagementService, ILogger<EntityRenameCommand> logger, ISceneManagementService scenes, IEngineRunner engine)
    {
        _entityManagementService = entityManagementService;
        _logger = logger;
        _scenes = scenes;
        _engine = engine;
    }

    public IEditorHistoryEntry? CreateHistoryEntry(EntityRenameCommandTarget target, EntityRenameCommandResult result)
    {
        if (!result.Changed) return null;
        var scene = _scenes.CurrentScene.Value;
        if (scene == null || !ReferenceEquals(scene.GetById(target.Entity.Id), target.Entity))
            throw new InvalidOperationException("The renamed entity does not belong to the current scene.");
        return new RenameHistoryEntry(this, _scenes, scene, target.Entity, result);
    }

    private bool CanExecute(GameEntity entity) => !_engine.IsPlaymodeActive.Value && !_engine.IsEngineStarting.Value
        && !(_engine.IsActive.Value && _engine.ActiveMode == EngineRunMode.PlayMode)
        && ReferenceEquals(_scenes.CurrentScene.Value?.GetById(entity.Id), entity);

    public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target)
    {
        if (!CanExecute(target.Entity)) throw new InvalidOperationException("The entity cannot be renamed in the current Editor context.");
        _logger.Log($"Renaming entity {target.Entity.Id}:{target.Entity.Name} -> {target.Name}");
        var beforeName = target.Entity.Name;
        _entityManagementService.RenameEntity(target.Entity, target.Name);
        return Task.FromResult(new EntityRenameCommandResult(beforeName, target.Entity.Name));
    }
}
