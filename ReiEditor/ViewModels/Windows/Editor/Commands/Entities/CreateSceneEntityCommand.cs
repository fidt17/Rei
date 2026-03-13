using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.ViewModels.Windows.Editor.Commands.Entities;

public class CreateSceneEntityCommand : ICommand, IDisposable
{
	public event EventHandler? CanExecuteChanged;

	private readonly ISceneManagementService _sceneManagement;
	private readonly IEntityManagementService _entityManagementService;

	public CreateSceneEntityCommand(ISceneManagementService sceneManagement, IEntityManagementService entityManagementService)
	{
		_sceneManagement = sceneManagement;
		_entityManagementService = entityManagementService;
		
		_sceneManagement.CurrentScene.Subscribe(HandleCurrentSceneChanged);
	}

	public void Dispose()
	{
		_sceneManagement.CurrentScene.Unsubscribe(HandleCurrentSceneChanged);
	}

	private void HandleCurrentSceneChanged(Scene? scene) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

	public bool CanExecute(object? parameter) => _sceneManagement.CurrentScene.Value != null;

	public async void Execute(object? parameter) => await CreateEntity();

	public Task<GameEntity?> CreateEntity(string? entityName = null)
	{
		return _entityManagementService.CreateEntity(entityName ?? "New Entity");
	}
}
