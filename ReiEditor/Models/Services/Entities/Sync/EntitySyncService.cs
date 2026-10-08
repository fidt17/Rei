using System;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntitySyncService : IEntitySyncService, IDisposable
{
    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ISceneManagementService _sceneManagement;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IEntityStateApplier _entityStateApplier;
    private readonly BehaviourSyncService _behaviourSyncService;

    public EntitySyncService(
        IAssetImporter assetImporter,
        IEntityApi entityApi,
        ISceneManagementService sceneManagement,
        IEngineRunner engineRunner,
        IBehaviourComponentsService behaviourComponentsService,
        BehaviourSyncService behaviourSyncService,
        IEntityStateApplier entityStateApplier)
    {
        _assetImporter = assetImporter;
        _entityApi = entityApi;
        _sceneManagement = sceneManagement;
        _engineRunner = engineRunner;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourSyncService = behaviourSyncService;
        _entityStateApplier = entityStateApplier;

        _behaviourComponentsService.BehaviourPropertyChangedEvent += _behaviourSyncService.WriteChangedProperty;
    }

    public void Dispose()
    {
        _behaviourComponentsService.BehaviourPropertyChangedEvent -= _behaviourSyncService.WriteChangedProperty;
    }

    public void UpdateEntityState(GameEntity entity)
    {
        if (!_engineRunner.IsActive.Value) return;
        if (_assetImporter.IsImporting.Value) return;

        var state = _entityApi.GetEntityData(entity.Id);
        if (state == null) return;
        var needsHierarchyRefresh = _entityStateApplier.Apply(entity, state);

        if (needsHierarchyRefresh)
        {
            _sceneManagement.CurrentScene.Value!.RebuildHierarchy();
        }
    }

}
