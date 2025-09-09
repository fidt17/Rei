using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Scenes;

public class SceneStateSynchronizer : ISceneStateSynchronizer
{
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IEntityManagementService _entityManagementService;

    public SceneStateSynchronizer(ISceneManagementService sceneManagementService, IEntityManagementService entityManagementService)
    {
        _sceneManagementService = sceneManagementService;
        _entityManagementService = entityManagementService;
    }

    public void SynchronizeStateWithEngine()
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null) return;
			
        foreach (var e in scene.Entities)
        {
            _entityManagementService.UpdateEntityState(e);
        }
    }
}