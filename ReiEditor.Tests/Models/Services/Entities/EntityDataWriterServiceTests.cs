using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies editor property writes and runtime entity-data request payloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class EntityDataWriterServiceTests
{
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

    private sealed class TestEntityApi : IEntityApi
    {
        public SetEntityDataRequest? SetDataRequest { get; private set; }
        public bool ThrowOnSetData { get; init; }

        public void SetData(SetEntityDataRequest request)
        {
            if (ThrowOnSetData) throw new InvalidOperationException("set failed");
            SetDataRequest = request;
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
    /// Offline scalar writes update the requested property and avoid the engine API.
    /// </summary>
    [Fact]
    public void OfflineScalarWriteUpdatesEditorProperty()
    {
        var api = new TestEntityApi();
        var service = new EntityDataWriterService(new TestEngineRunner(false), api);
        var entity = EntityWithIntegerProperty(8, "speed", 2);

        var result = service.SetBehaviourProperty(entity, 8, "speed", 7);

        Assert.True(result);
        Assert.Equal(7, entity.GetBehaviour(8)!.GetProperty("speed").Value);
        Assert.Null(api.SetDataRequest);
    }

    /// <summary>
    /// Offline writes reject missing behaviours and properties without mutating the entity.
    /// </summary>
    [Theory]
    [InlineData(9, "speed")]
    [InlineData(8, "missing")]
    public void OfflineWriteRejectsMissingTarget(int behaviourId, string propertyName)
    {
        var entity = EntityWithIntegerProperty(8, "speed", 2);
        var service = new EntityDataWriterService(new TestEngineRunner(false), new TestEntityApi());

        var result = service.SetBehaviourProperty(entity, behaviourId, propertyName, 7);

        Assert.False(result);
        Assert.Equal(2, entity.GetBehaviour(8)!.GetProperty("speed").Value);
    }

    /// <summary>
    /// Offline nested writes recursively update every named leaf in the existing property tree.
    /// </summary>
    [Fact]
    public void OfflineNestedWriteUpdatesExistingLeaves()
    {
        var entity = new GameEntity(12, "Entity");
        var behaviour = new BehaviourComponent(4);
        var root = Custom("bounds", null);
        var min = Custom("min", root);
        var x = Integer("x", 1, min);
        min.Value = new Dictionary<string, SerializedProperty> { ["x"] = x };
        root.Value = new Dictionary<string, SerializedProperty> { ["min"] = min };
        behaviour.AddProperty(root);
        entity.AddBehaviour(behaviour);
        var service = new EntityDataWriterService(new TestEngineRunner(false), new TestEntityApi());

        var result = service.SetBehaviourProperty(entity, 4, "bounds", new Dictionary<string, object?>
        {
            ["min"] = new Dictionary<string, object?> { ["x"] = 9 }
        });

        Assert.True(result);
        Assert.Equal(9, x.Value);
    }

    /// <summary>
    /// Runtime scalar writes send entity and behaviour IDs with one Value wrapper.
    /// </summary>
    [Fact]
    public void RuntimeScalarWriteBuildsWrappedPayload()
    {
        var api = new TestEntityApi();
        var service = new EntityDataWriterService(new TestEngineRunner(true), api);

        var result = service.SetBehaviourProperty(new GameEntity(31, "Runtime"), 6, "speed", 3.5f);

        Assert.True(result);
        var request = Assert.IsType<SetEntityDataRequest>(api.SetDataRequest);
        Assert.Equal(31, request.SceneId);
        var behaviour = Assert.Single(request.Behaviours);
        Assert.Equal(6, behaviour[SetEntityDataRequest.REI_BEHAVIOUR_ID]);
        var value = Assert.IsType<Dictionary<string, object?>>(behaviour["speed"]);
        Assert.Equal(3.5f, Assert.IsType<float>(value["Value"]));
    }

    /// <summary>
    /// Runtime nested writes wrap each nested dictionary and scalar at its protocol level.
    /// </summary>
    [Fact]
    public void RuntimeNestedWriteBuildsRecursiveValueWrappers()
    {
        var api = new TestEntityApi();
        var service = new EntityDataWriterService(new TestEngineRunner(true), api);

        service.SetBehaviourProperty(new GameEntity(31, "Runtime"), 6, "bounds", new Dictionary<string, object?>
        {
            ["min"] = new Dictionary<string, object?> { ["x"] = 5 }
        });

        var behaviour = Assert.Single(api.SetDataRequest!.Behaviours);
        var bounds = Assert.IsType<Dictionary<string, object?>>(behaviour["bounds"]);
        var boundsValue = Assert.IsType<Dictionary<string, object?>>(bounds["Value"]);
        var min = Assert.IsType<Dictionary<string, object?>>(boundsValue["min"]);
        var minValue = Assert.IsType<Dictionary<string, object?>>(min["Value"]);
        var x = Assert.IsType<Dictionary<string, object?>>(minValue["x"]);
        Assert.Equal(5, x["Value"]);
    }

    /// <summary>
    /// Runtime API failures propagate to the caller instead of reporting a successful write.
    /// </summary>
    [Fact]
    public void RuntimeWritePropagatesApiFailure()
    {
        var service = new EntityDataWriterService(new TestEngineRunner(true), new TestEntityApi { ThrowOnSetData = true });

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.SetBehaviourProperty(new GameEntity(1, "Entity"), 2, "speed", 3));

        Assert.Equal("set failed", error.Message);
    }

    private static GameEntity EntityWithIntegerProperty(int behaviourId, string name, int value)
    {
        var entity = new GameEntity(12, "Entity");
        var behaviour = new BehaviourComponent(behaviourId);
        behaviour.AddProperty(Integer(name, value, null));
        entity.AddBehaviour(behaviour);
        return entity;
    }

    private static SerializedProperty Integer(string name, int value, SerializedProperty? parent)
        => new(name, SerializedTypeEnum.Integer, value, "int", parent);

    private static SerializedProperty Custom(string name, SerializedProperty? parent)
        => new(name, SerializedTypeEnum.Custom, null, "Object", parent);
}
