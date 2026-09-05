using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;
using System.Diagnostics.CodeAnalysis;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies offline entity editing and immediate runtime entity-management replies.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class EntityManagementServiceTests
{
    private sealed class TestSceneManagementService(Scene? scene) : ISceneManagementService
    {
        public ReiEditor.Utils.Common.IObservable<Scene?> CurrentScene { get; } = new Observable<Scene?>(scene);
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<Scene?> CreateScene(string name, string projectPath) => throw new NotSupportedException();
        public Task LoadScene(Scene scene) => throw new NotSupportedException();
        public Task ReloadCurrentScene() => throw new NotSupportedException();
        public BuildScenesConfiguration GetBuildConfiguration() => throw new NotSupportedException();
        public void SetBuildSceneId(Scene scene, int id) => throw new NotSupportedException();
    }

    private sealed class TestEngineRunner(bool active) : IEngineRunner
    {
        public event Action EngineStartedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public event Action EngineStartFailedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public ReiEditor.Utils.Common.IObservable<bool> IsActive { get; } = new Observable<bool>(active);
        public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive { get; } = new Observable<bool>(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive { get; } = new Observable<bool>(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting { get; } = new Observable<bool>(false);
        public EngineRunMode ActiveMode => default;
        public bool StartEngine(EngineRunMode mode) => throw new NotSupportedException();
        public Task StopEngine() => throw new NotSupportedException();
    }

    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) { behaviour = null; return false; }
        public int? GetIdByName(string name) => name == EngineBehavioursConstants.TRANSFORM ? 1 : null;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    private sealed class TestBehaviourComponentsService : IBehaviourComponentsService
    {
        public event Action<EntityBehaviourPropertyChangeEventArgs>? BehaviourPropertyChangedEvent
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }
        public List<int> AddedIds { get; } = new();
        public List<int> DeletedIds { get; } = new();

        public bool AddComponent(GameEntity entity, int behaviourId)
        {
            AddedIds.Add(behaviourId);
            var component = new BehaviourComponent(behaviourId);
            if (behaviourId == 1)
            {
                component.AddProperty(Integer(EngineBehavioursConstants.TRANSFORM_PARENT));
                component.AddProperty(Integer(EngineBehavioursConstants.TRANSFORM_ORDER));
            }
            entity.AddBehaviour(component);
            return true;
        }

        public bool DeleteComponent(GameEntity entity, BehaviourComponent component)
        {
            DeletedIds.Add(component.Id);
            entity.DeleteBehaviour(component);
            return true;
        }

        public bool TryGetRequiringComponent(GameEntity entity, int requiredBehaviourId, out string requiringComponentName)
        {
            requiringComponentName = "";
            return false;
        }

        public void ApplySerializedValue(SerializedProperty property, object? value) => property.Value = value;
        public void RefreshComponents(GameEntity entity) { }

        private static SerializedProperty Integer(string name)
            => new(name, SerializedTypeEnum.Integer, 0, "int", null);
    }

    private sealed class TestEntityApi : IEntityApi
    {
        public InstantiateEntityResponse? CreateResponse { get; init; }
        public GetEntityDataResponse? EntityData { get; init; }
        public Exception? CreateException { get; init; }
        public List<(int EntityId, string Name)> Renames { get; } = new();
        public List<(int EntityId, int BehaviourId)> AddedBehaviours { get; } = new();
        public List<(int EntityId, int BehaviourId)> DeletedBehaviours { get; } = new();
        public List<int> DestroyedIds { get; } = new();

        public InstantiateEntityResponse? CreateNewEntity(string name)
        {
            if (CreateException != null) throw CreateException;
            return CreateResponse;
        }

        public GetEntityDataResponse? GetEntityData(int sceneEntityId) => EntityData;
        public void Rename(int sceneEntityId, string newName) => Renames.Add((sceneEntityId, newName));
        public void AddBehaviour(int sceneEntityId, int behaviourId) => AddedBehaviours.Add((sceneEntityId, behaviourId));
        public void DeleteBehaviour(int sceneEntityId, int behaviourId) => DeletedBehaviours.Add((sceneEntityId, behaviourId));
        public void DestroyEntity(int sceneEntityId) => DestroyedIds.Add(sceneEntityId);
        public GetSceneEntitiesResponse? GetSceneEntities() => throw new InvalidOperationException("polling is forbidden in immediate-response test");
        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order) => throw new NotSupportedException();
        public void SetData(SetEntityDataRequest request) => throw new NotSupportedException();
        public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw new NotSupportedException();
        public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw new NotSupportedException();
        public void SetEntitySelection(SetEntitySelectionRequest request) => throw new NotSupportedException();
        public void ResetEntitySelection() => throw new NotSupportedException();
    }

    private sealed class TestEntitySyncService : IEntitySyncService
    {
        public List<GameEntity> UpdatedEntities { get; } = new();
        public void UpdateEntityState(GameEntity entity) => UpdatedEntities.Add(entity);
    }

    private sealed class TestRectTransformLayoutService : IRectTransformLayoutService
    {
        public RectTransformVector2 GetParentSize(GameEntity entity) => throw new NotSupportedException();
        public bool TryGetRectTransform(GameEntity entity, out BehaviourComponent rectTransform) { rectTransform = null!; return false; }
        public bool TryReadLayout(BehaviourComponent rectTransform, out RectTransformLayoutData data) { data = default; return false; }
        public bool TryPreserveRectForPivot(GameEntity entity, BehaviourComponent rectTransform, float pivotX, float pivotY, out RectTransformLayoutData preservedLayout) { preservedLayout = default; return false; }
        public bool TryPreserveRectForParent(GameEntity entity, GameEntity? newParent, out RectTransformLayoutData preservedLayout) { preservedLayout = default; return false; }
        public void ApplyLayoutToEditor(BehaviourComponent rectTransform, RectTransformLayoutData layout) => throw new NotSupportedException();
        public Dictionary<string, object?> SerializeVector2(RectTransformVector2 value) => throw new NotSupportedException();
    }

    /// <summary>
    /// Offline creation allocates the next ID, creates Transform, appends the root, and mirrors hierarchy metadata into properties.
    /// </summary>
    [Fact]
    public async Task OfflineCreateBuildsTransformAndAppendsRoot()
    {
        var scene = new Scene("Scene");
        var existing = new GameEntity(4, "Existing");
        existing.Transform.SetOrder(3);
        scene.AddEntity(existing);
        var components = new TestBehaviourComponentsService();
        var service = CreateService(scene, false, components: components);

        var created = await service.CreateEntity("Created");

        Assert.NotNull(created);
        Assert.Equal(5, created.Id);
        Assert.Equal(1, created.Transform.Order);
        Assert.Equal(0, created.Transform.Parent);
        Assert.Equal(new[] { 1 }, components.AddedIds);
        Assert.Equal(0, created.GetBehaviour(1)!.GetProperty(EngineBehavioursConstants.TRANSFORM_PARENT).Value);
        Assert.Equal(1, created.GetBehaviour(1)!.GetProperty(EngineBehavioursConstants.TRANSFORM_ORDER).Value);
        Assert.Same(created, scene.GetById(5));
    }

    /// <summary>
    /// Offline creation with a parent inserts the entity after existing children and synchronizes Transform fields.
    /// </summary>
    [Fact]
    public async Task OfflineCreateWithParentAppendsChild()
    {
        var scene = new Scene("Scene");
        var parent = new GameEntity(1, "Parent");
        scene.AddEntity(parent);
        var firstChild = new GameEntity(2, "Child");
        scene.AddEntity(firstChild);
        scene.MoveEntity(firstChild, parent, 0);
        var service = CreateService(scene, false);

        var created = await service.CreateEntity("Created", parent);

        Assert.NotNull(created);
        Assert.Equal(parent.Id, created.Transform.Parent);
        Assert.Equal(1, created.Transform.Order);
        Assert.Equal(parent.Id, created.GetBehaviour(1)!.GetProperty(EngineBehavioursConstants.TRANSFORM_PARENT).Value);
        Assert.Equal(1, created.GetBehaviour(1)!.GetProperty(EngineBehavioursConstants.TRANSFORM_ORDER).Value);
    }

    /// <summary>
    /// Offline rename substitutes the default ID-based name and behaviour operations delegate to the component service.
    /// </summary>
    [Fact]
    public void OfflineRenameAndBehaviourChangesMutateEditorEntity()
    {
        var scene = new Scene("Scene");
        var entity = new GameEntity(7, "Old");
        scene.AddEntity(entity);
        var components = new TestBehaviourComponentsService();
        var service = CreateService(scene, false, components: components);

        service.RenameEntity(entity, "");
        service.AddBehaviour(entity, 5);
        service.DeleteBehaviour(entity, 5);

        Assert.Equal("Entity 7", entity.Name);
        Assert.Equal(new[] { 5 }, components.AddedIds);
        Assert.Equal(new[] { 5 }, components.DeletedIds);
    }

    /// <summary>
    /// Offline destruction removes the target subtree from both scene storage and hierarchy.
    /// </summary>
    [Fact]
    public void OfflineDestroyRemovesSubtree()
    {
        var scene = new Scene("Scene");
        var parent = new GameEntity(1, "Parent");
        var child = new GameEntity(2, "Child");
        scene.AddEntity(parent);
        scene.AddEntity(child);
        scene.MoveEntity(child, parent, 0);
        var service = CreateService(scene, false);

        service.DestroyEntity(parent);

        Assert.Empty(scene.Entities);
        Assert.Null(scene.Hierarchy.GetNode(parent));
        Assert.Null(scene.Hierarchy.GetNode(child));
    }

    /// <summary>
    /// Runtime creation with an immediate ID response adds one synchronized entity without entering polling fallback.
    /// </summary>
    [Fact]
    public async Task RuntimeCreateUsesImmediateIdResponse()
    {
        var scene = new Scene("Scene");
        var api = new TestEntityApi
        {
            CreateResponse = new InstantiateEntityResponse { EntityId = 42 },
            EntityData = new GetEntityDataResponse { EntityId = 42, Name = "Runtime" }
        };
        var sync = new TestEntitySyncService();
        var service = CreateService(scene, true, api, sync);

        var created = await service.CreateEntity("Requested");

        Assert.NotNull(created);
        Assert.Equal(42, created.Id);
        Assert.Equal("Runtime", created.Name);
        Assert.Same(created, scene.GetById(42));
        Assert.Same(created, Assert.Single(sync.UpdatedEntities));
    }

    /// <summary>
    /// Runtime creation preserves an existing entity instance returned for the immediate ID.
    /// </summary>
    [Fact]
    public async Task RuntimeCreatePreservesExistingEntityIdentity()
    {
        var scene = new Scene("Scene");
        var existing = new GameEntity(42, "Existing");
        scene.AddEntity(existing);
        var api = new TestEntityApi
        {
            CreateResponse = new InstantiateEntityResponse { EntityId = 42 },
            EntityData = new GetEntityDataResponse { EntityId = 42, Name = "Engine" }
        };
        var sync = new TestEntitySyncService();
        var service = CreateService(scene, true, api, sync);

        var created = await service.CreateEntity("Requested");

        Assert.Same(existing, created);
        Assert.Empty(sync.UpdatedEntities);
    }

    /// <summary>
    /// Runtime rename and behaviour operations call the API and request immediate state refreshes.
    /// </summary>
    [Fact]
    public void RuntimeEditsCallApiAndRefreshEntity()
    {
        var scene = new Scene("Scene");
        var entity = new GameEntity(7, "Old");
        entity.AddBehaviour(new BehaviourComponent(5));
        scene.AddEntity(entity);
        var api = new TestEntityApi();
        var sync = new TestEntitySyncService();
        var service = CreateService(scene, true, api, sync);

        service.RenameEntity(entity, "New");
        service.AddBehaviour(entity, 6);
        service.DeleteBehaviour(entity, 5);
        service.DestroyEntity(entity);

        Assert.Equal(new[] { (7, "New") }, api.Renames);
        Assert.Equal(new[] { (7, 6) }, api.AddedBehaviours);
        Assert.Equal(new[] { (7, 5) }, api.DeletedBehaviours);
        Assert.Equal(new[] { 7 }, api.DestroyedIds);
        Assert.Equal(new[] { entity, entity, entity }, sync.UpdatedEntities);
        Assert.Null(scene.GetById(7));
    }

    /// <summary>
    /// Runtime creation API failures are logged and returned as null.
    /// </summary>
    [Fact]
    public async Task RuntimeCreateFailureLogsAndReturnsNull()
    {
        var logger = new TestLogger<EntityManagementService>();
        var service = CreateService(new Scene("Scene"), true,
            new TestEntityApi { CreateException = new InvalidOperationException("create failed") }, logger: logger);

        var created = await service.CreateEntity("Requested");

        Assert.Null(created);
        Assert.Contains(logger.Entries, entry => entry.Message == "create failed");
    }

    /// <summary>
    /// Offline creation without a current scene logs the failure and returns null.
    /// </summary>
    [Fact]
    public async Task OfflineCreateWithoutSceneLogsAndReturnsNull()
    {
        var logger = new TestLogger<EntityManagementService>();
        var service = CreateService(null, false, logger: logger);

        var created = await service.CreateEntity("Requested");

        Assert.Null(created);
        Assert.Contains(logger.Entries, entry => entry.Message == "Current scene is missing");
    }

    private static EntityManagementService CreateService(
        Scene? scene,
        bool active,
        TestEntityApi? api = null,
        TestEntitySyncService? sync = null,
        TestBehaviourComponentsService? components = null,
        TestLogger<EntityManagementService>? logger = null)
        => new(
            logger ?? new TestLogger<EntityManagementService>(),
            new TestSceneManagementService(scene),
            components ?? new TestBehaviourComponentsService(),
            api ?? new TestEntityApi(),
            new TestBehaviourRegistry(),
            new TestEngineRunner(active),
            sync ?? new TestEntitySyncService(),
            new TestRectTransformLayoutService());
}
