using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies runtime entity instantiation synchronization and parent-change layout orchestration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class EntityManagementAdvancedTests
{
    /// <summary>Captures only explicitly supported entity API calls and rejects every unrelated call.</summary>
    private sealed class TestEntityApi : IEntityApi
    {
        public Func<InstantiateEntityRequest, InstantiateEntityResponse?>? OnInstantiate { get; set; }
        public Func<GetSceneEntitiesResponse?>? OnGetSceneEntities { get; set; }
        public Func<int, GetEntityDataResponse?>? OnGetEntityData { get; set; }
        public Action<int, int, int>? OnSetParent { get; set; }
        public Action<SetEntityDataRequest>? OnSetData { get; set; }
        public List<InstantiateEntityRequest> InstantiateRequests { get; } = new();
        public List<int> StateRequests { get; } = new();
        public List<string> Calls { get; } = new();
        public int SnapshotRequests { get; private set; }

        public GetSceneEntitiesResponse? GetSceneEntities()
        {
            Calls.Add("snapshot");
            SnapshotRequests++;
            return (OnGetSceneEntities ?? throw UnexpectedCall())();
        }

        public GetEntityDataResponse? GetEntityData(int sceneEntityId)
        {
            Calls.Add($"state:{sceneEntityId}");
            StateRequests.Add(sceneEntityId);
            return (OnGetEntityData ?? throw UnexpectedCall())(sceneEntityId);
        }

        public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request)
        {
            Calls.Add("instantiate");
            InstantiateRequests.Add(request);
            return (OnInstantiate ?? throw UnexpectedCall())(request);
        }

        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order)
        {
            Calls.Add("set-parent");
            (OnSetParent ?? throw UnexpectedCall())(sceneEntityId, parentSceneEntityId, order);
        }

        public void SetData(SetEntityDataRequest request)
        {
            Calls.Add("set-data");
            (OnSetData ?? throw UnexpectedCall())(request);
        }

        public InstantiateEntityResponse? CreateNewEntity(string name) => throw UnexpectedCall();
        public void DestroyEntity(int sceneEntityId) => throw UnexpectedCall();
        public void Rename(int sceneEntityId, string newName) => throw UnexpectedCall();
        public void AddBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
        public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
        public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw UnexpectedCall();
        public void SetEntitySelection(SetEntitySelectionRequest request) => throw UnexpectedCall();
        public void ResetEntitySelection() => throw UnexpectedCall();

        private static InvalidOperationException UnexpectedCall([System.Runtime.CompilerServices.CallerMemberName] string? member = null)
            => new($"Unexpected IEntityApi.{member} call.");
    }

    /// <summary>Records entity refresh order and optionally applies test transform state.</summary>
    private sealed class TestEntitySyncService : IEntitySyncService
    {
        public Action<GameEntity>? OnUpdate { get; set; }
        public List<GameEntity> UpdatedEntities { get; } = new();

        public void UpdateEntityState(GameEntity entity)
        {
            UpdatedEntities.Add(entity);
            OnUpdate?.Invoke(entity);
        }
    }

    /// <summary>Controls rect preservation results and records editor layout application.</summary>
    private sealed class TestRectTransformLayoutService : IRectTransformLayoutService
    {
        public bool PreserveResult { get; set; }
        public bool HasRectTransform { get; set; }
        public RectTransformLayoutData PreservedLayout { get; set; }
        public BehaviourComponent RectTransform { get; set; } = new(91);
        public Action<string>? OnCall { get; set; }
        public List<string> Calls { get; } = new();
        public GameEntity? PreserveEntity { get; private set; }
        public GameEntity? PreserveParent { get; private set; }
        public RectTransformLayoutData? AppliedLayout { get; private set; }

        public bool TryPreserveRectForParent(GameEntity entity, GameEntity? newParent, out RectTransformLayoutData preservedLayout)
        {
            Calls.Add("preserve");
            OnCall?.Invoke("preserve");
            PreserveEntity = entity;
            PreserveParent = newParent;
            preservedLayout = PreservedLayout;
            return PreserveResult;
        }

        public bool TryGetRectTransform(GameEntity entity, out BehaviourComponent rectTransform)
        {
            Calls.Add("get-rect");
            OnCall?.Invoke("get-rect");
            rectTransform = RectTransform;
            return HasRectTransform;
        }

        public void ApplyLayoutToEditor(BehaviourComponent rectTransform, RectTransformLayoutData layout)
        {
            Calls.Add("apply-editor");
            OnCall?.Invoke("apply-editor");
            AppliedLayout = layout;
        }

        public Dictionary<string, object?> SerializeVector2(RectTransformVector2 value)
            => new() { ["x"] = value.X, ["y"] = value.Y };

        public RectTransformVector2 GetParentSize(GameEntity entity) => throw new NotSupportedException();
        public bool TryReadLayout(BehaviourComponent rectTransform, out RectTransformLayoutData data) => throw new NotSupportedException();
        public bool TryPreserveRectForPivot(GameEntity entity, BehaviourComponent rectTransform, float pivotX, float pivotY, out RectTransformLayoutData preservedLayout)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Inactive runtime rejects instantiation before any entity API access.
    /// </summary>
    [Fact]
    public void InstantiateRejectsInactiveEngineBeforeApiAccess()
    {
        var api = new TestEntityApi();
        var logger = new TestLogger<EntityManagementService>();
        var service = CreateService(new Scene("Scene"), false, api, logger: logger);

        var result = service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Null(result);
        Assert.Empty(api.Calls);
        Assert.Contains(logger.Entries, entry => entry.Message == "Instantiate entity requires engine to be running");
    }

    /// <summary>
    /// Default instantiation request carries source ID, source name, and child inclusion.
    /// </summary>
    [Fact]
    public void InstantiateBuildsDefaultRequestFromSource()
    {
        var api = ApiForSnapshot(31);
        var service = CreateService(new Scene("Scene"), true, api);

        var result = service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Equal(31, result);
        var request = Assert.Single(api.InstantiateRequests);
        Assert.Equal(4, request.SourceEntityId);
        Assert.Equal("Source", request.RequestedName);
        Assert.True(request.IncludeChildren);
    }

    /// <summary>
    /// Explicit duplicate name receives next available suffix and preserves include-children false.
    /// </summary>
    [Fact]
    public void InstantiateMakesRequestedNameUniqueAndForwardsChildFlag()
    {
        var scene = new Scene("Scene");
        scene.AddEntity(new GameEntity(1, "Clone"));
        scene.AddEntity(new GameEntity(2, "Clone 1"));
        var api = ApiForSnapshot(8, 1, 2);
        var service = CreateService(scene, true, api);

        service.InstantiateEntity(new GameEntity(4, "Source"), "Clone", includeChildren: false);

        var request = Assert.Single(api.InstantiateRequests);
        Assert.Equal("Clone 2", request.RequestedName);
        Assert.False(request.IncludeChildren);
    }

    /// <summary>
    /// Whitespace requested name falls back to source name before uniqueness is calculated.
    /// </summary>
    [Fact]
    public void InstantiateWhitespaceNameUsesUniqueSourceName()
    {
        var scene = new Scene("Scene");
        scene.AddEntity(new GameEntity(1, "Source"));
        var api = ApiForSnapshot(8, 1);
        var service = CreateService(scene, true, api);

        service.InstantiateEntity(new GameEntity(4, "Source"), "   ");

        Assert.Equal("Source 1", Assert.Single(api.InstantiateRequests).RequestedName);
    }

    /// <summary>
    /// Missing current scene still sends base request but skips scene snapshot synchronization.
    /// </summary>
    [Fact]
    public void InstantiateWithoutSceneReturnsResponseAndSkipsSnapshot()
    {
        var api = new TestEntityApi { OnInstantiate = _ => new InstantiateEntityResponse { EntityId = 12 } };
        var service = CreateService(null, true, api);

        var result = service.InstantiateEntity(new GameEntity(4, "Source"), "Clone");

        Assert.Equal(12, result);
        Assert.Equal("Clone", Assert.Single(api.InstantiateRequests).RequestedName);
        Assert.Equal(0, api.SnapshotRequests);
    }

    /// <summary>
    /// Null scene snapshot leaves editor scene unchanged without requesting entity states.
    /// </summary>
    [Fact]
    public void InstantiateNullSnapshotLeavesSceneUnchanged()
    {
        var scene = new Scene("Scene");
        var api = new TestEntityApi
        {
            OnInstantiate = _ => new InstantiateEntityResponse { EntityId = 8 },
            OnGetSceneEntities = () => null
        };
        var service = CreateService(scene, true, api);

        var result = service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Equal(8, result);
        Assert.Empty(scene.Entities);
        Assert.Empty(api.StateRequests);
    }

    /// <summary>
    /// Snapshot containing only preexisting IDs avoids redundant state refreshes.
    /// </summary>
    [Fact]
    public void InstantiateSnapshotWithoutNewIdsSkipsStateReads()
    {
        var scene = new Scene("Scene");
        scene.AddEntity(new GameEntity(1, "Existing"));
        var api = ApiForSnapshot(8, 1);
        var sync = new TestEntitySyncService();
        var service = CreateService(scene, true, api, sync);

        service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Empty(api.StateRequests);
        Assert.Empty(sync.UpdatedEntities);
        Assert.Single(scene.Entities);
    }

    /// <summary>
    /// New snapshot IDs refresh parent before child and rebuild matching hierarchy links.
    /// </summary>
    [Fact]
    public void InstantiateSyncsNewParentBeforeChildAndRebuildsHierarchy()
    {
        var scene = new Scene("Scene");
        var api = ApiForSnapshot(20, 21, 20);
        api.OnGetEntityData = id => TransformState(id, id == 21 ? 20 : 0, id == 21 ? 2 : 1);
        var sync = new TestEntitySyncService
        {
            OnUpdate = entity =>
            {
                if (entity.Id == 20) { entity.Transform.SetParent(0); entity.Transform.SetOrder(1); }
                if (entity.Id == 21) { entity.Transform.SetParent(20); entity.Transform.SetOrder(2); }
            }
        };
        var service = CreateService(scene, true, api, sync);

        service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Equal(new[] { 20, 21 }, sync.UpdatedEntities.Select(entity => entity.Id));
        var parent = scene.GetById(20)!;
        var child = scene.GetById(21)!;
        Assert.Same(parent, scene.Hierarchy.GetNode(child)!.Parent!.Content);
    }

    /// <summary>
    /// Entity materialized during API call keeps object identity while still receiving state refresh.
    /// </summary>
    [Fact]
    public void InstantiatePreservesEntityMaterializedDuringApiCall()
    {
        var scene = new Scene("Scene");
        var materialized = new GameEntity(9, "Engine object");
        var api = ApiForSnapshot(9, 9);
        api.OnInstantiate = _ =>
        {
            scene.AddEntity(materialized);
            return new InstantiateEntityResponse { EntityId = 9 };
        };
        var sync = new TestEntitySyncService();
        var service = CreateService(scene, true, api, sync);

        service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Same(materialized, scene.GetById(9));
        Assert.Same(materialized, Assert.Single(sync.UpdatedEntities));
    }

    /// <summary>
    /// Instantiate API exception is logged, returns null, and prevents snapshot access.
    /// </summary>
    [Fact]
    public void InstantiateApiFailureLogsAndSkipsSnapshot()
    {
        var api = new TestEntityApi { OnInstantiate = _ => throw new InvalidOperationException("instantiate failed") };
        var logger = new TestLogger<EntityManagementService>();
        var service = CreateService(new Scene("Scene"), true, api, logger: logger);

        var result = service.InstantiateEntity(new GameEntity(4, "Source"));

        Assert.Null(result);
        Assert.Equal(0, api.SnapshotRequests);
        Assert.Contains(logger.Entries, entry => entry.Message == "instantiate failed");
    }

    /// <summary>
    /// Self-parent request exits before layout, API, or synchronization work.
    /// </summary>
    [Fact]
    public void SetParentIgnoresSelfParentRequest()
    {
        var entity = new GameEntity(4, "Entity");
        var api = new TestEntityApi();
        var rect = new TestRectTransformLayoutService();
        var sync = new TestEntitySyncService();
        var service = CreateService(new Scene("Scene"), true, api, sync, rect);

        service.SetParent(entity, entity, 2);

        Assert.Empty(api.Calls);
        Assert.Empty(rect.Calls);
        Assert.Empty(sync.UpdatedEntities);
    }

    /// <summary>
    /// Inactive runtime ignores parent changes before layout or API work.
    /// </summary>
    [Fact]
    public void SetParentIgnoresInactiveEngine()
    {
        var api = new TestEntityApi();
        var rect = new TestRectTransformLayoutService();
        var service = CreateService(new Scene("Scene"), false, api, rect: rect);

        service.SetParent(new GameEntity(4, "Entity"), new GameEntity(5, "Parent"), 2);

        Assert.Empty(api.Calls);
        Assert.Empty(rect.Calls);
    }

    /// <summary>
    /// Parent change without preserved rect sends target parent and order then refreshes target entity.
    /// </summary>
    [Fact]
    public void SetParentWithoutPreservedRectMovesThenRefreshesEntity()
    {
        var entity = new GameEntity(4, "Entity");
        var parent = new GameEntity(5, "Parent");
        var trace = new List<string>();
        var api = new TestEntityApi();
        (int Entity, int Parent, int Order)? call = null;
        api.OnSetParent = (entityId, parentId, order) =>
        {
            call = (entityId, parentId, order);
            trace.Add("set-parent");
        };
        var sync = new TestEntitySyncService { OnUpdate = _ => trace.Add("sync") };
        var rect = new TestRectTransformLayoutService { PreserveResult = false, OnCall = trace.Add };
        var service = CreateService(new Scene("Scene"), true, api, sync, rect);

        service.SetParent(entity, parent, 3);

        Assert.True(call.HasValue);
        Assert.Equal((4, 5, 3), call.Value);
        Assert.Equal(new[] { "preserve" }, rect.Calls);
        Assert.Same(entity, Assert.Single(sync.UpdatedEntities));
        Assert.Equal(new[] { "set-parent" }, api.Calls);
        Assert.Equal(new[] { "preserve", "set-parent", "sync" }, trace);
    }

    /// <summary>
    /// Preserved rect is applied to editor and serialized to runtime before entity refresh.
    /// </summary>
    [Fact]
    public void SetParentAppliesAndSerializesPreservedRect()
    {
        var entity = new GameEntity(4, "Entity");
        var parent = new GameEntity(5, "Parent");
        var layout = Layout();
        var trace = new List<string>();
        var rect = new TestRectTransformLayoutService
        {
            PreserveResult = true,
            HasRectTransform = true,
            PreservedLayout = layout,
            RectTransform = new BehaviourComponent(91),
            OnCall = trace.Add
        };
        SetEntityDataRequest? data = null;
        var api = new TestEntityApi
        {
            OnSetParent = (_, _, _) => trace.Add("set-parent"),
            OnSetData = request =>
            {
                trace.Add("set-data");
                data = request;
            }
        };
        var sync = new TestEntitySyncService { OnUpdate = _ => trace.Add("sync") };
        var service = CreateService(new Scene("Scene"), true, api, sync, rect);

        service.SetParent(entity, parent, 3);

        Assert.Same(entity, rect.PreserveEntity);
        Assert.Same(parent, rect.PreserveParent);
        Assert.Equal(layout, rect.AppliedLayout);
        Assert.Equal(new[] { "preserve", "get-rect", "apply-editor" }, rect.Calls);
        Assert.Equal(new[] { "set-parent", "set-data" }, api.Calls);
        Assert.Equal(new[] { "preserve", "set-parent", "get-rect", "apply-editor", "set-data", "sync" }, trace);
        Assert.NotNull(data);
        Assert.Equal(4, data.SceneId);
        var behaviour = Assert.Single(data.Behaviours);
        Assert.Equal(6, behaviour.Count);
        Assert.Equal(91, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        AssertVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, 0.1f, 0.2f);
        AssertVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, 0.8f, 0.9f);
        AssertVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, 0.3f, 0.7f);
        AssertVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, 10f, 20f);
        AssertVector(behaviour, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, 80f, 40f);
        Assert.Same(entity, Assert.Single(sync.UpdatedEntities));
    }

    /// <summary>
    /// Successful preservation without rect component skips layout write but still refreshes entity.
    /// </summary>
    [Fact]
    public void SetParentMissingRectComponentSkipsLayoutWrite()
    {
        var entity = new GameEntity(4, "Entity");
        (int Entity, int Parent, int Order)? call = null;
        var api = new TestEntityApi { OnSetParent = (entityId, parentId, order) => call = (entityId, parentId, order) };
        var rect = new TestRectTransformLayoutService { PreserveResult = true, HasRectTransform = false };
        var sync = new TestEntitySyncService();
        var service = CreateService(new Scene("Scene"), true, api, sync, rect);

        service.SetParent(entity, null, 0);

        Assert.True(call.HasValue);
        Assert.Equal((4, 0, 0), call.Value);
        Assert.Equal(new[] { "set-parent" }, api.Calls);
        Assert.Equal(new[] { "preserve", "get-rect" }, rect.Calls);
        Assert.Same(entity, Assert.Single(sync.UpdatedEntities));
    }

    /// <summary>
    /// Parent API exception is logged and prevents layout write and entity refresh.
    /// </summary>
    [Fact]
    public void SetParentApiFailureLogsAndStopsRemainingWork()
    {
        var api = new TestEntityApi { OnSetParent = (_, _, _) => throw new InvalidOperationException("parent failed") };
        var rect = new TestRectTransformLayoutService { PreserveResult = true, HasRectTransform = true, PreservedLayout = Layout() };
        var sync = new TestEntitySyncService();
        var logger = new TestLogger<EntityManagementService>();
        var service = CreateService(new Scene("Scene"), true, api, sync, rect, logger);

        service.SetParent(new GameEntity(4, "Entity"), null, 0);

        Assert.Equal(new[] { "preserve" }, rect.Calls);
        Assert.Empty(sync.UpdatedEntities);
        Assert.Contains(logger.Entries, entry => entry.Message == "parent failed");
    }

    private static TestEntityApi ApiForSnapshot(int responseId, params int[] snapshotIds)
        => new()
        {
            OnInstantiate = _ => new InstantiateEntityResponse { EntityId = responseId },
            OnGetSceneEntities = () => new GetSceneEntitiesResponse
            {
                Entities = snapshotIds.Select(id => new GetSceneEntitiesResponse.SceneEntitiesResponseEntity { Id = id }).ToList()
            },
            OnGetEntityData = id => TransformState(id, 0, 0)
        };

    private static GetEntityDataResponse TransformState(int id, int parent, int order)
        => new()
        {
            EntityId = id,
            Name = $"Entity {id}",
            Behaviours = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM,
                    [EngineBehavioursConstants.TRANSFORM_PARENT] = parent,
                    [EngineBehavioursConstants.TRANSFORM_ORDER] = order
                }
            }
        };

    private static RectTransformLayoutData Layout()
        => new(new(0.1f, 0.2f), new(0.8f, 0.9f), new(0.3f, 0.7f), new(10f, 20f), new(80f, 40f));

    /// <summary>Verifies one serialized rect vector has exactly expected coordinates.</summary>
    private static void AssertVector(IReadOnlyDictionary<string, object?> behaviour, string property, float x, float y)
    {
        var vector = Assert.IsType<Dictionary<string, object?>>(behaviour[property]);
        Assert.Equal(2, vector.Count);
        Assert.Equal(x, vector["x"]);
        Assert.Equal(y, vector["y"]);
    }

    private static EntityManagementService CreateService(
        Scene? scene,
        bool active,
        TestEntityApi api,
        TestEntitySyncService? sync = null,
        TestRectTransformLayoutService? rect = null,
        TestLogger<EntityManagementService>? logger = null)
    {
        var sceneManagement = new TestSceneManagementService();
        sceneManagement.Scene.Value = scene;
        var engine = new TestEngineRunner();
        engine.Active.Value = active;

        return new EntityManagementService(
            logger ?? new TestLogger<EntityManagementService>(),
            sceneManagement,
            new TestBehaviourComponentsService(),
            api,
            new TestBehaviourRegistry(),
            engine,
            sync ?? new TestEntitySyncService(),
            rect ?? new TestRectTransformLayoutService());
    }
}
