using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReiEditor.Models.EditorApp.Commands;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

public class CreateSceneEntityCommand : ICommand, IDisposable, IUndoableEditorCommand<string, GameEntity?>
{
    private sealed class CreateHistoryEntry(CreateSceneEntityCommand command, Scene scene, GameEntity entity) : IEditorHistoryEntry
    {
        private readonly string _name = entity.Name;

        public bool CanUndo => command.CanEdit(scene) && ReferenceEquals(scene.GetById(entity.Id), entity)
            && entity.Name == _name && !entity.Transform.HasParent() && entity.Behaviours.Count() == 1
            && scene.Hierarchy.GetNode(entity) is { } node && !node.ChildNodes.Any();

        public bool CanRedo => command.CanEdit(scene) && scene.GetById(entity.Id) == null;

        public Task UndoAsync()
        {
            if (!CanUndo) throw new InvalidOperationException("The created root entity cannot be removed.");
            command._entityManagementService.DestroyEntity(entity);
            if (scene.GetById(entity.Id) != null) throw new InvalidOperationException("The created entity was not removed.");
            return Task.CompletedTask;
        }

        public async Task RedoAsync()
        {
            if (!CanRedo || await command._entityManagementService.CreateEntity(_name, existingEntity: entity) != entity)
                throw new InvalidOperationException("The created root entity could not be restored.");
        }
    }

    public event EventHandler? CanExecuteChanged;

    private readonly ISceneManagementService _sceneManagement;
    private readonly IEntityManagementService _entityManagementService;
    private readonly IEditorCommandService _commands;
    private readonly IEngineRunner _engine;

    public CreateSceneEntityCommand(ISceneManagementService sceneManagement, IEntityManagementService entityManagementService,
        IEditorCommandService commands, IEngineRunner engine)
    {
        _sceneManagement = sceneManagement;
        _entityManagementService = entityManagementService;
        _commands = commands;
        _engine = engine;
        _sceneManagement.CurrentScene.Subscribe(HandleCurrentSceneChanged);
    }

    public void Dispose() => _sceneManagement.CurrentScene.Unsubscribe(HandleCurrentSceneChanged);

    private void HandleCurrentSceneChanged(Scene? scene) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private bool CanEdit(Scene scene) => ReferenceEquals(_sceneManagement.CurrentScene.Value, scene)
        && !_engine.IsEngineStarting.Value && !_engine.IsPlaymodeActive.Value
        && !(_engine.IsActive.Value && _engine.ActiveMode == EngineRunMode.PlayMode);

    public bool CanExecute(object? parameter) => _sceneManagement.CurrentScene.Value is { } scene && CanEdit(scene) && !_commands.IsBusy;

    public async void Execute(object? parameter) => await CreateEntity();

    public Task<GameEntity?> CreateEntity(string? entityName = null, GameEntity? parent = null)
    {
        var name = entityName ?? "New Entity";
        if (parent != null) return _entityManagementService.CreateEntity(name, parent);
        return CanExecute(null) ? _commands.ExecuteAsync(this, name) : Task.FromResult<GameEntity?>(null);
    }

    Task<GameEntity?> IEditorCommand<string, GameEntity?>.ExecuteAsync(string name) => _entityManagementService.CreateEntity(name);

    IEditorHistoryEntry? IUndoableEditorCommand<string, GameEntity?>.CreateHistoryEntry(string name, GameEntity? result)
    {
        var scene = _sceneManagement.CurrentScene.Value;
        return result != null && scene != null && ReferenceEquals(scene.GetById(result.Id), result)
            ? new CreateHistoryEntry(this, scene, result)
            : null;
    }
}
