using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>
/// Verifies property-change gates and protocol payloads sent during behaviour synchronization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "EntitySync")]
public sealed class BehaviourSyncServiceTests
{
    private sealed class TestAssetImporter(bool importing) : IAssetImporter
    {
        public event Action ImportedAssetsEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(importing);
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
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

    private sealed class TestEntityStateApplier(bool applying) : IEntityStateApplier
    {
        public bool IsApplyingEngineState { get; } = applying;
        public bool Apply(GameEntity entity, GetEntityDataResponse state) => throw new NotSupportedException();
    }

    private sealed class TestEntityApi : IEntityApi
    {
        public SetEntityDataRequest? Request { get; private set; }
        public bool ThrowOnSetData { get; init; }

        public void SetData(SetEntityDataRequest request)
        {
            if (ThrowOnSetData) throw new InvalidOperationException("write failed");
            Request = request;
        }

        public GetSceneEntitiesResponse? GetSceneEntities() => throw UnexpectedCall();
        public GetEntityDataResponse? GetEntityData(int sceneEntityId) => throw UnexpectedCall();
        public InstantiateEntityResponse? CreateNewEntity(string name) => throw UnexpectedCall();
        public void DestroyEntity(int sceneEntityId) => throw UnexpectedCall();
        public void Rename(int sceneEntityId, string newName) => throw UnexpectedCall();
        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order) => throw UnexpectedCall();
        public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw UnexpectedCall();
        public void AddBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
        public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw UnexpectedCall();
        public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw UnexpectedCall();
        public void SetEntitySelection(SetEntitySelectionRequest request) => throw UnexpectedCall();
        public void ResetEntitySelection() => throw UnexpectedCall();
        private static InvalidOperationException UnexpectedCall() => new("Unexpected API call");
    }

    /// <summary>
    /// Inactive engine, active import, and engine-state application each suppress outgoing writes.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void WriteIsSuppressedBySynchronizationGates(bool active, bool importing, bool applying)
    {
        var api = new TestEntityApi();
        var service = CreateService(active, importing, applying, api, new TestLogger<EntitySyncService>());
        var args = ScalarChange();

        service.WriteChangedProperty(args);

        Assert.Null(api.Request);
    }

    /// <summary>
    /// Scalar changes send entity and behaviour IDs plus a single Value wrapper.
    /// </summary>
    [Fact]
    public void ScalarChangeSendsWrappedValue()
    {
        var api = new TestEntityApi();
        var service = CreateService(true, false, false, api, new TestLogger<EntitySyncService>());

        service.WriteChangedProperty(ScalarChange());

        var request = Assert.IsType<SetEntityDataRequest>(api.Request);
        Assert.Equal(20, request.SceneId);
        var behaviour = Assert.Single(request.Behaviours);
        Assert.Equal(3, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        var value = Assert.IsType<Dictionary<string, object?>>(behaviour["speed"]);
        Assert.Equal(6, value["Value"]);
    }

    /// <summary>
    /// Nested leaf changes send the root custom property with recursive Value wrappers.
    /// </summary>
    [Fact]
    public void NestedLeafChangeSendsRootCustomProperty()
    {
        var entity = new GameEntity(20, "Entity");
        var component = new BehaviourComponent(3);
        var root = new SerializedProperty("position", SerializedTypeEnum.Custom, null, "Vector2", null);
        var x = new SerializedProperty("x", SerializedTypeEnum.Integer, 6, "int", root);
        root.Value = new Dictionary<string, SerializedProperty> { ["x"] = x };
        component.AddProperty(root);
        entity.AddBehaviour(component);
        var api = new TestEntityApi();
        var service = CreateService(true, false, false, api, new TestLogger<EntitySyncService>());

        service.WriteChangedProperty(new EntityBehaviourPropertyChangeEventArgs(entity, component, x));

        var behaviour = Assert.Single(api.Request!.Behaviours);
        Assert.False(behaviour.ContainsKey("x"));
        var rootPayload = Assert.IsType<Dictionary<string, object?>>(behaviour["position"]);
        var children = Assert.IsType<Dictionary<string, object?>>(rootPayload["Value"]);
        var xPayload = Assert.IsType<Dictionary<string, object?>>(children["x"]);
        Assert.Equal(6, xPayload["Value"]);
    }

    /// <summary>
    /// Collection item changes send the collection root with a Value list and raw scalar items.
    /// </summary>
    [Fact]
    public void CollectionItemChangeSendsRootCollectionProperty()
    {
        var entity = new GameEntity(20, "Entity");
        var component = new BehaviourComponent(3);
        var items = new SerializedProperty("items", SerializedTypeEnum.Collection, null, "vector<int>", null);
        var first = new SerializedProperty("[0]", SerializedTypeEnum.Integer, 4, "int", items);
        var second = new SerializedProperty("[1]", SerializedTypeEnum.Integer, 8, "int", items);
        items.Value = new List<SerializedProperty> { first, second };
        component.AddProperty(items);
        entity.AddBehaviour(component);
        var api = new TestEntityApi();
        var service = CreateService(true, false, false, api, new TestLogger<EntitySyncService>());

        service.WriteChangedProperty(new EntityBehaviourPropertyChangeEventArgs(entity, component, second));

        var behaviour = Assert.Single(api.Request!.Behaviours);
        var collectionPayload = Assert.IsType<Dictionary<string, object?>>(behaviour["items"]);
        var values = Assert.IsType<List<object?>>(collectionPayload["Value"]);
        Assert.Equal(new object?[] { 4, 8 }, values);
    }

    /// <summary>
    /// API exceptions are logged and do not escape the property-change callback.
    /// </summary>
    [Fact]
    public void ApiFailureIsLoggedWithoutEscaping()
    {
        var logger = new TestLogger<EntitySyncService>();
        var service = CreateService(true, false, false, new TestEntityApi { ThrowOnSetData = true }, logger);

        var error = Record.Exception(() => service.WriteChangedProperty(ScalarChange()));

        Assert.Null(error);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("write failed", StringComparison.Ordinal));
    }

    private static BehaviourSyncService CreateService(bool active, bool importing, bool applying, IEntityApi api, TestLogger<EntitySyncService> logger)
        => new(new TestAssetImporter(importing), new TestEngineRunner(active), api, logger, new TestEntityStateApplier(applying));

    private static EntityBehaviourPropertyChangeEventArgs ScalarChange()
    {
        var entity = new GameEntity(20, "Entity");
        var component = new BehaviourComponent(3);
        var property = new SerializedProperty("speed", SerializedTypeEnum.Integer, 6, "int", null);
        component.AddProperty(property);
        entity.AddBehaviour(component);
        return new EntityBehaviourPropertyChangeEventArgs(entity, component, property);
    }
}
