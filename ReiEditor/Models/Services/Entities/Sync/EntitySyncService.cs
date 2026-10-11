using System;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntitySyncService : IEntitySyncService, IDisposable
{
    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IEntityStateApplier _entityStateApplier;
    private readonly BehaviourSyncService _behaviourSyncService;

    public EntitySyncService(
        IAssetImporter assetImporter,
        IEntityApi entityApi,
        IEngineRunner engineRunner,
        IBehaviourComponentsService behaviourComponentsService,
        BehaviourSyncService behaviourSyncService,
        IEntityStateApplier entityStateApplier)
    {
        _assetImporter = assetImporter;
        _entityApi = entityApi;
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
        _entityStateApplier.Apply(entity, state);
    }

}
