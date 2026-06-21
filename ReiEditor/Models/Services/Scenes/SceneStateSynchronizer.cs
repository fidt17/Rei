using ReiEditor.Models.Services.Entities.Sync;

namespace ReiEditor.Models.Services.Scenes;

public class SceneStateSynchronizer : ISceneStateSynchronizer
{
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IEntitySyncService _entitySyncService;

    public SceneStateSynchronizer(ISceneManagementService sceneManagementService, IEntitySyncService entitySyncService)
    {
        _sceneManagementService = sceneManagementService;
        _entitySyncService = entitySyncService;
    }

    public void SynchronizeStateWithEngine()
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        if (scene == null) return;
			
        foreach (var e in scene.Entities)
        {
            _entitySyncService.UpdateEntityState(e);
        }
    }
}