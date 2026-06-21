using System;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public class EntitySyncService : IEntitySyncService, IDisposable
{
    private CancellationTokenSource? _sceneUpdateTaskCTS;

    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ISceneManagementService _sceneManagement;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IEntityStateApplier _entityStateApplier;
    private readonly BehaviourSyncService _behaviourSyncService;
    private readonly ISceneSyncService _sceneSyncService;

    public EntitySyncService(
        IAssetImporter assetImporter,
        IEntityApi entityApi,
        ISceneManagementService sceneManagement,
        IEngineRunner engineRunner,
        IBehaviourComponentsService behaviourComponentsService,
        BehaviourSyncService behaviourSyncService,
        ISceneSyncService sceneSyncService, IEntityStateApplier entityStateApplier)
    {
        _assetImporter = assetImporter;
        _entityApi = entityApi;
        _sceneManagement = sceneManagement;
        _engineRunner = engineRunner;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourSyncService = behaviourSyncService;
        _sceneSyncService = sceneSyncService;
        _entityStateApplier = entityStateApplier;

        _behaviourComponentsService.BehaviourPropertyChangedEvent += _behaviourSyncService.WriteChangedProperty;
        _engineRunner.IsActive.Subscribe(HandleEngineActiveChanged, invoke: false);
    }

    public void Dispose()
    {
        _sceneUpdateTaskCTS?.Cancel();
        _sceneUpdateTaskCTS?.Dispose();
        _sceneUpdateTaskCTS = null;

        _behaviourComponentsService.BehaviourPropertyChangedEvent -= _behaviourSyncService.WriteChangedProperty;
        _engineRunner.IsActive.Unsubscribe(HandleEngineActiveChanged);
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

    private void HandleEngineActiveChanged(bool isActive)
    {
        if (isActive)
        {
            _sceneUpdateTaskCTS?.Cancel();
            _sceneUpdateTaskCTS = new CancellationTokenSource();

            var token = _sceneUpdateTaskCTS.Token;
            Task.Run(async () =>
            {
                while (_engineRunner.IsActive.Value && !token.IsCancellationRequested)
                {
                    await Task.Delay(33, token);
                    _sceneSyncService.SynchronizeWithEngine();
                }
            }, token);
        }
        else
        {
            _sceneUpdateTaskCTS?.Cancel();
        }
    }
}
