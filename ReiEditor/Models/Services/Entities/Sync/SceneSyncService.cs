using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public class SceneSyncService : ISceneSyncService
{
    private const int ENTITY_STATE_SYNC_INTERVAL_MS = 250;

    private readonly IAssetImporter _assetImporter;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly ISceneManagementService _sceneManagement;
    private readonly ILogger<EntitySyncService> _logger;
    private readonly EngineSelectionSyncService _selectionSyncService;
    private readonly IEntityStateApplier _entityStateApplier;
    private readonly Stopwatch _stateSyncStopwatch = Stopwatch.StartNew();

    public SceneSyncService(
        IAssetImporter assetImporter,
        IEngineRunner engineRunner,
        IEntityApi entityApi,
        ISceneManagementService sceneManagement,
        ILogger<EntitySyncService> logger,
        EngineSelectionSyncService selectionSyncService,
        IEntityStateApplier entityStateApplier)
    {
        _assetImporter = assetImporter;
        _engineRunner = engineRunner;
        _entityApi = entityApi;
        _sceneManagement = sceneManagement;
        _logger = logger;
        _selectionSyncService = selectionSyncService;
        _entityStateApplier = entityStateApplier;
    }

    public void SynchronizeWithEngine()
    {
        try
        {
            if (!_engineRunner.IsActive.Value) return;
            if (_assetImporter.IsImporting.Value) return;

            var selectionBeforeSync = _selectionSyncService.CaptureEditorSelectedEntityIds();
            var entities = _entityApi.GetSceneEntities();
            if (entities == null) return;

            var scene = _sceneManagement.CurrentScene.Value;
            if (scene == null) throw new Exception("Current scene is missing");

            var currentSceneEntities = scene.Entities.ToList();
            var didUpdateSelection = _selectionSyncService.TryApplySelectionSnapshot(currentSceneEntities, entities, selectionBeforeSync);
            var entitySetChanged = HasEntitySetChanged(currentSceneEntities, entities);
            if (!entitySetChanged && _stateSyncStopwatch.ElapsedMilliseconds < ENTITY_STATE_SYNC_INTERVAL_MS) return;

            _stateSyncStopwatch.Restart();

            var entityStates = LoadEntityStates(entities, out var parentByEntityId, out var orderByEntityId);
            var selectedEntityIds = new HashSet<int>();
            var needsHierarchyRefresh = ApplyEntityStates(scene, currentSceneEntities, entities, entityStates, parentByEntityId, orderByEntityId, selectedEntityIds);

            if (needsHierarchyRefresh)
            {
                scene.RebuildHierarchy();
            }

            if (!didUpdateSelection && selectionBeforeSync.SetEquals(_selectionSyncService.CaptureEditorSelectedEntityIds()))
            {
                _selectionSyncService.ApplySelection(scene.Entities.ToList(), selectedEntityIds);
            }
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }

    private static bool HasEntitySetChanged(IReadOnlyCollection<GameEntity> currentSceneEntities, GetSceneEntitiesResponse entities)
    {
        if (currentSceneEntities.Count != entities.Entities.Count) return true;

        var currentEntityIds = currentSceneEntities.Select(x => x.Id).ToHashSet();
        return entities.Entities.Any(x => !currentEntityIds.Contains(x.Id));
    }

    private Dictionary<int, GetEntityDataResponse?> LoadEntityStates(
        GetSceneEntitiesResponse entities,
        out Dictionary<int, int> parentByEntityId,
        out Dictionary<int, int> orderByEntityId)
    {
        var entityStates = new Dictionary<int, GetEntityDataResponse?>();
        parentByEntityId = new Dictionary<int, int>();
        orderByEntityId = new Dictionary<int, int>();

        foreach (var entity in entities.Entities)
        {
            var state = _entityApi.GetEntityData(entity.Id);
            entityStates[entity.Id] = state;

            var parentId = 0;
            var order = 0;
            if (state != null && EntitySyncUtility.TryGetTransformData(state.Behaviours, out var transformParent, out var transformOrder))
            {
                parentId = transformParent ?? 0;
                order = transformOrder ?? 0;
            }

            parentByEntityId[entity.Id] = parentId;
            orderByEntityId[entity.Id] = order;
        }

        return entityStates;
    }

    private bool ApplyEntityStates(
        Scene scene,
        IReadOnlyCollection<GameEntity> currentSceneEntities,
        GetSceneEntitiesResponse entities,
        IReadOnlyDictionary<int, GetEntityDataResponse?> entityStates,
        Dictionary<int, int> parentByEntityId,
        Dictionary<int, int> orderByEntityId,
        ISet<int> selectedEntityIds)
    {
        var needsHierarchyRefresh = false;
        var orderedEntityIds = EntitySyncUtility.BuildOrderedEntityIds(parentByEntityId, orderByEntityId);

        foreach (var entityId in orderedEntityIds)
        {
            var engineEntity = entities.Entities.Find(x => x.Id == entityId);
            if (engineEntity == null) continue;

            var gameEntity = scene.GetById(engineEntity.Id);
            if (gameEntity == null)
            {
                gameEntity = new GameEntity(engineEntity.Id, $"Entity {engineEntity}");
                scene.AddEntity(gameEntity);
                needsHierarchyRefresh = true;
            }

            var state = entityStates[entityId];
            if (state != null)
            {
                needsHierarchyRefresh |= _entityStateApplier.Apply(gameEntity, state);
            }

            if (engineEntity.IsSelected)
            {
                selectedEntityIds.Add(gameEntity.Id);
            }
        }

        foreach (var gameEntity in currentSceneEntities.Where(x => !entities.Entities.Exists(y => y.Id == x.Id)))
        {
            scene.DeleteEntity(gameEntity, refreshTransforms: false);
            needsHierarchyRefresh = true;
        }

        return needsHierarchyRefresh;
    }
}
