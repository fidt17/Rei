using System;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities;

public class EntityManagementService : IEntityManagementService
{
	private readonly ILogger<EntityManagementService> _logger;
	private readonly ISceneManagementService _sceneManagement;

	public EntityManagementService(ILogger<EntityManagementService> logger, ISceneManagementService sceneManagement)
	{
		_logger = logger;
		_sceneManagement = sceneManagement;
	}

	public GameEntity? CreateEntity(string name)
	{
		try
		{
			if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

			var s = _sceneManagement.CurrentScene.Value;
			var e = new GameEntity(s.AllocateEntityId(), name);
			s.AddEntity(e);

			_logger.Log($"Created {e}");
			return e;
		}
		catch (Exception exception)
		{
			_logger.LogException(exception);
		}

		return null;
	}
}