using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>
/// Verifies explicit entity-state updates and synchronization subscription cleanup without starting background polling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "EntitySync")]
public sealed class EntitySyncServiceTests
{
    private sealed class TestAssetImporter(bool importing) : IAssetImporter
    {
        public event Action ImportedAssetsEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(importing);
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
    }

    private sealed class TestEntityApi : IEntityApi
    {
        public GetEntityDataResponse? State { get; init; }
        public int GetDataCalls { get; private set; }
        public int SetDataCalls { get; private set; }

        public GetEntityDataResponse? GetEntityData(int sceneEntityId)
        {
            GetDataCalls++;
            return State;
        }

        public void SetData(SetEntityDataRequest request) => SetDataCalls++;
        public GetSceneEntitiesResponse? GetSceneEntities() => throw new NotSupportedException();
        public InstantiateEntityResponse? CreateNewEntity(string name) => throw new NotSupportedException();
        public void DestroyEntity(int sceneEntityId) => throw new NotSupportedException();
        public void Rename(int sceneEntityId, string newName) => throw new NotSupportedException();
        public void SetEntityParent(int sceneEntityId, int parentSceneEntityId, int order) => throw new NotSupportedException();
        public InstantiateEntityResponse? InstantiateEntity(InstantiateEntityRequest request) => throw new NotSupportedException();
        public void AddBehaviour(int sceneEntityId, int behaviourId) => throw new NotSupportedException();
        public void DeleteBehaviour(int sceneEntityId, int behaviourId) => throw new NotSupportedException();
        public void SelectEntity(int sceneEntityId, bool resetCurrentSelection = true) => throw new NotSupportedException();
        public void SetEntitySelection(SetEntitySelectionRequest request) => throw new NotSupportedException();
        public void ResetEntitySelection() => throw new NotSupportedException();
    }

    private sealed class TestEntityStateApplier(bool hierarchyChanged) : IEntityStateApplier
    {
        public bool IsApplyingEngineState => false;
        public int ApplyCalls { get; private set; }
        public GetEntityDataResponse? AppliedState { get; private set; }

        public bool Apply(GameEntity entity, GetEntityDataResponse state)
        {
            ApplyCalls++;
            AppliedState = state;
            return hierarchyChanged;
        }
    }

    private sealed class TestSceneSyncService : ISceneSyncService
    {
        public void SynchronizeWithEngine() => throw new InvalidOperationException("Background sync must not start in these tests");
    }

    /// <summary>
    /// Inactive engine and active asset import each suppress entity-data reads.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UpdateIsSuppressedByRuntimeGates(bool active, bool importing)
    {
        var api = new TestEntityApi { State = new GetEntityDataResponse() };
        using var service = CreateService(active, importing, api, new TestEntityStateApplier(false));

        service.UpdateEntityState(new GameEntity(4, "Entity"));

        Assert.Equal(0, api.GetDataCalls);
    }

    /// <summary>
    /// Missing engine state stops after one data read without invoking the state applier.
    /// </summary>
    [Fact]
    public void MissingStateDoesNotInvokeApplier()
    {
        var api = new TestEntityApi();
        var applier = new TestEntityStateApplier(false);
        using var service = CreateService(true, false, api, applier);

        service.UpdateEntityState(new GameEntity(4, "Entity"));

        Assert.Equal(1, api.GetDataCalls);
        Assert.Equal(0, applier.ApplyCalls);
    }

    /// <summary>
    /// Available engine state is forwarded once without rebuilding hierarchy when transform metadata is unchanged.
    /// </summary>
    [Fact]
    public void UnchangedStateAppliesWithoutHierarchyRebuild()
    {
        var state = new GetEntityDataResponse { EntityId = 4, Name = "Entity" };
        var api = new TestEntityApi { State = state };
        var applier = new TestEntityStateApplier(false);
        var scene = new Scene("Scene");
        var rebuilds = 0;
        scene.HierarchyRebuiltEvent += () => rebuilds++;
        using var service = CreateService(true, false, api, applier, scene: scene);

        service.UpdateEntityState(new GameEntity(4, "Entity"));

        Assert.Equal(1, applier.ApplyCalls);
        Assert.Same(state, applier.AppliedState);
        Assert.Equal(0, rebuilds);
    }

    /// <summary>
    /// State application requesting hierarchy refresh rebuilds the current scene exactly once.
    /// </summary>
    [Fact]
    public void ChangedTransformRebuildsCurrentScene()
    {
        var api = new TestEntityApi { State = new GetEntityDataResponse() };
        var scene = new Scene("Scene");
        var rebuilds = 0;
        scene.HierarchyRebuiltEvent += () => rebuilds++;
        using var service = CreateService(true, false, api, new TestEntityStateApplier(true), scene: scene);

        service.UpdateEntityState(new GameEntity(4, "Entity"));

        Assert.Equal(1, rebuilds);
    }

    /// <summary>
    /// Disposal detaches the behaviour-property writer so later component events produce no engine write.
    /// </summary>
    [Fact]
    public void DisposeUnsubscribesBehaviourPropertyWriter()
    {
        var api = new TestEntityApi();
        var components = new TestBehaviourComponentsService();
        var service = CreateService(true, false, api, new TestEntityStateApplier(false), components);
        var entity = new GameEntity(4, "Entity");
        var component = new BehaviourComponent(6);
        var property = new SerializedProperty("speed", SerializedTypeEnum.Integer, 3, "int", null);
        component.AddProperty(property);
        entity.AddBehaviour(component);
        var args = new EntityBehaviourPropertyChangeEventArgs(entity, component, property);

        components.Publish(args);
        service.Dispose();
        components.Publish(args);

        Assert.Equal(1, api.SetDataCalls);
    }

    private static EntitySyncService CreateService(
        bool active,
        bool importing,
        TestEntityApi api,
        TestEntityStateApplier applier,
        TestBehaviourComponentsService? components = null,
        Scene? scene = null)
    {
        var runner = new TestEngineRunner();
        runner.Active.Value = active;
        var sceneManagement = new TestSceneManagementService();
        sceneManagement.Scene.Value = scene;
        components ??= new TestBehaviourComponentsService();
        var importer = new TestAssetImporter(importing);
        var behaviourSync = new BehaviourSyncService(importer, runner, api, new TestLogger<EntitySyncService>(), applier);
        return new EntitySyncService(importer, api, sceneManagement, runner, components, behaviourSync, new TestSceneSyncService(), applier);
    }
}
