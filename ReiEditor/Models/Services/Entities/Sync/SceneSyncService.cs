using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Models.Services.Entities.Sync;

public sealed class SceneSyncService : ISceneSyncService, IDisposable
{
    private const int MAX_PACKETS_PER_BATCH = 16;

    private readonly object _gate = new();
    private readonly Queue<SceneSyncPacket> _packets = new();
    private readonly Dictionary<int, GameEntity> _removedForHistory = new();
    private readonly IEngineSceneSyncEventSource _source;
    private readonly ISceneManagementService _scenes;
    private readonly IEngineSelectionSyncService _selection;
    private readonly IEntitySyncService _entities;
    private readonly ILogger<SceneSyncService> _logger;
    private bool _scheduled;
    private bool _disposed;

    public SceneSyncService(IEngineSceneSyncEventSource source, ISceneManagementService scenes, IEngineSelectionSyncService selection, ILogger<SceneSyncService> logger, IEntitySyncService entities)
    {
        _source = source;
        _scenes = scenes;
        _selection = selection;
        _entities = entities;
        _logger = logger;
        _source.Received += Receive;
        _source.ConnectionReset += ClearQueue;
        _scenes.CurrentScene.Subscribe(HandleSceneChanged, invoke: false);
    }

    public void SynchronizeWithEngine() => _source.RequestSnapshot();

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        ClearQueue();
        _source.Received -= Receive;
        _source.ConnectionReset -= ClearQueue;
        _scenes.CurrentScene.Unsubscribe(HandleSceneChanged);
    }

    private void Receive(SceneSyncPacket packet)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _packets.Enqueue(packet);
            if (_scheduled) return;
            _scheduled = true;
        }
        Dispatcher.UIThread.Post(DrainQueue);
    }

    private void ClearQueue()
    {
        lock (_gate)
        {
            _packets.Clear();
            _removedForHistory.Clear();
        }
    }

    private void HandleSceneChanged(Scene? scene)
    {
        ClearQueue();
        if (scene != null) _source.RequestSnapshot();
    }

    private void DrainQueue()
    {
        for (var i = 0; i < MAX_PACKETS_PER_BATCH; i++)
        {
            var scene = _scenes.CurrentScene.Value;
            SceneSyncPacket packet;
            lock (_gate)
            {
                if (_disposed || !_packets.TryDequeue(out packet!)) break;
            }
            try
            {
                var previousScene = scene;
                if (packet.SceneAssetId != null && packet.SceneAssetId != scene?.AssetId)
                {
                    if (!packet.IsSnapshot) continue;
                    scene = _scenes.SynchronizeRuntimeScene(packet.SceneAssetId, packet.SceneName ?? packet.SceneAssetId);
                }
                if (scene == null) continue;
                Apply(scene, packet, refreshComponents: !ReferenceEquals(scene, previousScene));
            }
            catch (Exception error) { _logger.LogException(error); }
        }

        lock (_gate)
        {
            _scheduled = !_disposed && _packets.Count > 0;
            if (!_scheduled) return;
        }
        Dispatcher.UIThread.Post(DrainQueue);
    }

    private void Apply(Scene scene, SceneSyncPacket packet, bool refreshComponents)
    {
        var removedIds = packet.IsSnapshot
            ? scene.Entities.Select(entity => entity.Id).Except(packet.Entities.Select(entity => entity.Id)).ToHashSet()
            : packet.RemovedEntityIds.ToHashSet();
        removedIds.UnionWith(packet.HistoryRemovedEntityIds);
        var hierarchyChanged = false;
        foreach (var state in packet.Entities)
        {
            var entity = scene.GetById(state.Id);
            var syncComponents = refreshComponents || entity == null;
            if (entity == null)
            {
                lock (_gate) _removedForHistory.Remove(state.Id, out entity);
                entity ??= new GameEntity(state.Id, state.Name);
                scene.AddEntity(entity);
                hierarchyChanged = true;
            }
            entity.SetName(state.Name);
            hierarchyChanged |= entity.Transform.Parent != state.ParentId || entity.Transform.Order != state.Order;
            entity.Transform.SetParent(state.ParentId);
            entity.Transform.SetOrder(state.Order);
            if (syncComponents) _entities.UpdateEntityState(entity);
        }

        // Keep the models referenced by earlier history entries until their native entities return.
        foreach (var id in packet.HistoryRemovedEntityIds)
        {
            if (scene.GetById(id) is { } entity)
                lock (_gate) _removedForHistory[id] = entity;
        }

        // Scene.DeleteEntity removes a subtree. Detach children that survived in the engine first.
        if (removedIds.Count > 0)
        {
            foreach (var entity in scene.Entities)
            {
                if (removedIds.Contains(entity.Id)) continue;
                var node = scene.Hierarchy.GetNode(entity);
                if (node?.Parent != null && removedIds.Contains(node.Parent.Content.Id))
                    scene.Hierarchy.MoveNode(node, null, int.MaxValue);
            }
        }
        foreach (var id in removedIds)
        {
            var entity = scene.GetById(id);
            if (entity == null) continue;
            scene.DeleteEntity(entity, refreshTransforms: false);
            hierarchyChanged = true;
        }

        if (hierarchyChanged) scene.SynchronizeHierarchy();
        _selection.ApplySelection(scene.Entities, packet.SelectedEntityIds.ToHashSet());
    }
}
