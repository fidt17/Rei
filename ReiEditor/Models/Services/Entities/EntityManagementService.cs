using System;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities;

public class EntityManagementService : IEntityManagementService
{
    public event Action<GameEntity>? EntityCreatedEvent;
    public event Action<GameEntity>? EntityDeletedEvent;
	
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
            EntityCreatedEvent?.Invoke(e);
            return e;
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }

        return null;
    }

    public void RenameEntity(GameEntity e, string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name)) throw new Exception($"Invalid entity name [{name}]");
            e.SetName(name);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }

    public void DeleteEntity(GameEntity e)
    {
        try
        {
            if (_sceneManagement.CurrentScene.Value == null) throw new Exception("Current scene is missing");

            var s = _sceneManagement.CurrentScene.Value;
            s.DeleteEntity(e);

            _logger.Log($"Deleted {e}");
            EntityDeletedEvent?.Invoke(e);
        }
        catch (Exception exception)
        {
            _logger.LogException(exception);
        }
    }
}