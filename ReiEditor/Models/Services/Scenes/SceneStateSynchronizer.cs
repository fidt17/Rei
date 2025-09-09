using ReiEditor.Models.Services.Entities;

namespace ReiEditor.Models.Services.Scenes;

public class SceneStateSynchronizer : ISceneStateSynchronizer
{
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IEntityStateSynchronizer _entityStateSynchronizer;

    public SceneStateSynchronizer(ISceneManagementService sceneManagementService, IEntityStateSynchronizer entityStateSynchronizer)
    {
        _sceneManagementService = sceneManagementService;
        _entityStateSynchronizer = entityStateSynchronizer;
    }

    public void SynchronizeStateWithEngine()
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null) return;
			
        foreach (var e in scene.Entities)
        {
            _entityStateSynchronizer.UpdateEntityState(e);
        }
    }
}