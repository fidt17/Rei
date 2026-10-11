using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "SceneSync")]
public sealed class SceneSyncServiceTests
{
    private sealed class Properties : ISerializedPropertiesService
    {
        public void ApplyValue(SerializedProperty property, object? value) => property.Value = value;
        public SerializedProperty Create(string name, SerializableObjectInfo.SerializedPropertyData data, SerializedProperty? parent) => throw new NotSupportedException();
        public void Refresh(SerializedProperty property) => throw new NotSupportedException();
    }

    private sealed class Source : IEngineSceneSyncEventSource
    {
        public event Action<SceneSyncPacket>? Received;
        public event Action? ConnectionReset;
        public bool IsConnected { get; private set; } = true;
        public int SnapshotRequests { get; private set; }
        public void Connect() { IsConnected = true; ConnectionReset?.Invoke(); }
        public void Disconnect() { IsConnected = false; ConnectionReset?.Invoke(); }
        public void RequestSnapshot() { if (IsConnected) SnapshotRequests++; }
        public void Send(SceneSyncPacket packet) => Received?.Invoke(packet);
    }

    private sealed class Selectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    private readonly Source _source = new();
    private readonly TestSceneManagementService _scenes = new();
    private readonly TestEntityApi _api = new();
    private readonly TestLogger<SceneSyncService> _logger = new();
    private readonly Scene _scene = new("events");
    private readonly SelectionService _selection;
    private readonly List<ISelectable> _registered = new();

    public SceneSyncServiceTests()
    {
        _selection = new SelectionService(_api);
        _scenes.Scene.Value = _scene;
        _scene.Hierarchy.NodeAddedEvent += node =>
        {
            var selectable = new Selectable(node.Content);
            _registered.Add(selectable);
            _selection.RegisterSelectable(selectable);
        };
        _scene.Hierarchy.NodeRemovedEvent += node =>
        {
            var removed = new[] { node }.Concat(_scene.Hierarchy.GetAllChildNodes(node)).Select(item => item.Content).ToHashSet();
            foreach (var selectable in _registered.OfType<IEntitySelectable>().Where(item => removed.Contains(item.Entity)).ToArray())
            {
                _selection.UnregisterSelectable(selectable);
                _registered.Remove(selectable);
            }
        };
    }

    [AvaloniaFact]
    public void SnapshotCreatesWholeHierarchyBeforeApplyingSelectionWithoutQueriesOrEcho()
    {
        _scene.AddEntity(new GameEntity(9, "obsolete"));
        using var service = Create();
        Task.Run(() => _source.Send(Packet(true, new[] { State(2, 1, 0), State(1), State(3, 1, 1) }, selected: new[] { 2, 3 }))).GetAwaiter().GetResult();
        Assert.Single(_scene.Entities);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { 1, 2, 3 }, _scene.Entities.Select(entity => entity.Id).Order());
        Assert.Equal(new[] { 2, 3 }, _scene.Hierarchy.GetNode(_scene.GetById(1)!)!.ChildNodes.Select(node => node.Content.Id));
        Assert.Equal(new[] { 2, 3 }, Selected());
        Assert.Empty(_api.Selections);
        Assert.Empty(_logger.Entries);
    }

    [AvaloniaFact]
    public void ChangedMetadataPreservesBehaviourDataAndSelection()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1), State(2), State(3) }, selected: new[] { 2 }));
        Dispatcher.UIThread.RunJobs();
        var child = _scene.GetById(2)!;
        var behaviour = new BehaviourComponent(42);
        child.AddBehaviour(behaviour);
        _source.Send(Packet(false, new[] { new SceneEntityState(2, "renamed", 1, 5), State(3, 1, 0) }, selected: new[] { 2 }));
        Dispatcher.UIThread.RunJobs();
        Assert.Same(child, _scene.GetById(2));
        Assert.Equal("renamed", child.Name);
        Assert.Same(behaviour, Assert.Single(child.Behaviours));
        Assert.Equal(new[] { 3, 2 }, _scene.Hierarchy.GetNode(_scene.GetById(1)!)!.ChildNodes.Select(node => node.Content.Id));
        Assert.Equal(new[] { 2 }, Selected());
        Assert.Empty(_api.Selections);
    }

    [AvaloniaFact]
    public void EntityPropertyReadsCannotConsumeOrOverwriteHierarchyEvents()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1), State(2) }, selected: new[] { 2 }));
        Dispatcher.UIThread.RunJobs();
        var entity = _scene.GetById(2)!;
        var transform = new BehaviourComponent(1);
        transform.AddProperty(new SerializedProperty(EngineBehavioursConstants.TRANSFORM_PARENT, SerializedTypeEnum.Integer, 0, "int", null));
        transform.AddProperty(new SerializedProperty(EngineBehavioursConstants.TRANSFORM_ORDER, SerializedTypeEnum.Integer, 0, "int", null));
        entity.AddBehaviour(transform);
        var registry = new TestBehaviourRegistry();
        registry.Ids[EngineBehavioursConstants.TRANSFORM] = 1;
        var applier = new EntityStateApplier(new TestLogger<EntitySyncService>(), registry, new TestBehaviourComponentsService(), new Properties());
        var state = new GetEntityDataResponse
        {
            EntityId = 2, Name = "renamed",
            Behaviours = { new Dictionary<string, object>
            {
                ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM,
                [EngineBehavioursConstants.TRANSFORM_PARENT] = 1,
                [EngineBehavioursConstants.TRANSFORM_ORDER] = 3
            } }
        };
        var rebuilds = 0;
        _scene.HierarchyRebuiltEvent += () => rebuilds++;

        _source.Send(Packet(false, new[] { new SceneEntityState(2, "renamed", 1, 3) }, selected: new[] { 2 }));
        applier.Apply(entity, state);
        Assert.Equal(0, entity.Transform.Parent);
        Assert.Null(_scene.Hierarchy.GetNode(entity)!.Parent);
        Assert.Equal(0, rebuilds);
        Assert.Equal(1, transform.GetProperty(EngineBehavioursConstants.TRANSFORM_PARENT).Value);

        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, rebuilds);
        Assert.Same(_scene.GetById(1), _scene.Hierarchy.GetNode(entity)!.Parent!.Content);
        Assert.Equal(new[] { 2 }, Selected());
        Assert.Same(transform, entity.GetBehaviour(1));

        state.Behaviours[0][EngineBehavioursConstants.TRANSFORM_PARENT] = 0;
        state.Behaviours[0][EngineBehavioursConstants.TRANSFORM_ORDER] = 0;
        applier.Apply(entity, state);
        Assert.Equal("renamed", entity.Name);
        Assert.Equal(1, entity.Transform.Parent);
        Assert.Equal(3, entity.Transform.Order);
        Assert.Equal(0, rebuilds);
        Assert.Same(_scene.GetById(1), _scene.Hierarchy.GetNode(entity)!.Parent!.Content);
        Assert.Equal(new[] { 2 }, Selected());
        Assert.Empty(_api.Selections);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(3)]
    public void RemovingParentKeepsSurvivingChild(int newParent)
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1), State(2, 1), State(3) }));
        Dispatcher.UIThread.RunJobs();
        var child = _scene.GetById(2)!;
        _source.Send(Packet(false, new[] { State(2, newParent) }, new[] { 1 }, new[] { 2 }));
        Dispatcher.UIThread.RunJobs();
        Assert.Same(child, _scene.GetById(2));
        Assert.Null(_scene.GetById(1));
        Assert.Equal(newParent, child.Transform.Parent);
        Assert.Equal(newParent == 0 ? null : _scene.GetById(3), _scene.Hierarchy.GetNode(child)!.Parent?.Content);
        Assert.Equal(new[] { 2 }, Selected());
    }

    [AvaloniaFact]
    public void BurstIsAppliedInFifoOrderAndYieldsBetweenBatches()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1) }));
        Dispatcher.UIThread.RunJobs();
        var names = new List<string>();
        _scene.GetById(1)!.NameChangedEvent += (_, name) => names.Add(name);
        for (var i = 1; i <= 64; i++)
            _source.Send(Packet(false, new[] { new SceneEntityState(1, $"name-{i}", 0, 0) }));
        var appliedBeforeNextUiJob = 0;
        Dispatcher.UIThread.Post(() => appliedBeforeNextUiJob = names.Count);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(16, appliedBeforeNextUiJob);
        Assert.Equal(Enumerable.Range(1, 64).Select(i => $"name-{i}"), names);
        Assert.Empty(_logger.Entries);
    }

    [AvaloniaFact]
    public void EmptySnapshotClearsSceneAndSelection()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1), State(2, 1) }, selected: new[] { 2 }));
        Dispatcher.UIThread.RunJobs();
        _source.Send(Packet(true, Array.Empty<SceneEntityState>()));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(_scene.Entities);
        Assert.Empty(Selected());
        Assert.Empty(_api.Selections);
    }

    [AvaloniaFact]
    public void RestartDiscardsQueuedOldPacketsAndAcceptsNewSnapshot()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1) }));
        _source.Disconnect();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(_scene.Entities);
        _source.Connect();
        _source.Send(Packet(true, new[] { State(2) }));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, Assert.Single(_scene.Entities).Id);
    }

    [AvaloniaFact]
    public void SceneChangeAndExplicitReloadRequestFreshSnapshot()
    {
        using var service = Create();
        _source.Send(Packet(true, new[] { State(1) }));
        _scenes.Scene.Value = new Scene("next");
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(_scenes.Scene.Value.Entities);
        Assert.Equal(1, _source.SnapshotRequests);
        service.SynchronizeWithEngine();
        Assert.Equal(2, _source.SnapshotRequests);
    }

    [AvaloniaFact]
    public void DisposeDiscardsQueuedPacketsAndUnsubscribes()
    {
        var service = Create();
        _source.Send(Packet(true, new[] { State(1) }));
        service.Dispose();
        _source.Send(Packet(true, new[] { State(2) }));
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(_scene.Entities);
    }

    private SceneSyncService Create() => new(_source, _scenes, new EngineSelectionSyncService(_selection), _logger);
    private int[] Selected() => _selection.SelectedItems.OfType<IEntitySelectable>().Select(item => item.Entity.Id).Order().ToArray();
    private static SceneEntityState State(int id, int parent = 0, int order = 0) => new(id, $"entity-{id}", parent, order);
    private static SceneSyncPacket Packet(bool snapshot, SceneEntityState[] entities, int[]? removed = null, int[]? selected = null) => new()
    {
        IsSnapshot = snapshot, Entities = entities,
        RemovedEntityIds = removed ?? Array.Empty<int>(), SelectedEntityIds = selected ?? Array.Empty<int>()
    };
}
