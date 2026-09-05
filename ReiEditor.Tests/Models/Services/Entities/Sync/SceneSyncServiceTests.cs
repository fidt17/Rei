using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>Verifies immediate scene snapshot reconciliation without stopwatch delays or engine polling.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "SceneSync")]
public sealed class SceneSyncServiceTests
{
    private sealed class TestImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public Observable<bool> Importing { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting => Importing;
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
    }

    private readonly TestImporter _importer = new();
    private readonly TestEngineRunner _runner = new();
    private readonly TestEntityApi _api = new();
    private readonly TestSceneManagementService _scenes = new();
    private readonly TestLogger<EntitySyncService> _logger = new();

    /// <summary>Inactive engine, active import, null snapshots and API exceptions preserve the existing scene.</summary>
    [AvaloniaTheory]
    [InlineData("inactive")]
    [InlineData("importing")]
    [InlineData("null")]
    [InlineData("exception")]
    public void UnavailableSnapshotsDoNotMutateScene(string state)
    {
        var scene = new Scene("original");
        var entity = new GameEntity(1, "kept");
        scene.AddEntity(entity);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = state != "inactive";
        _importer.Importing.Value = state == "importing";
        var calls = 0;
        _api.OnGetSceneEntities = () => { calls++; return state == "exception" ? throw new IOException("api") : null; };

        CreateService().SynchronizeWithEngine();
        Assert.Same(entity, Assert.Single(scene.Entities));
        Assert.Equal(state is "inactive" or "importing" ? 0 : 1, calls);
        if (state == "exception") Assert.IsType<IOException>(Assert.Single(_logger.Entries).Exception);
        else Assert.Empty(_logger.Entries);
    }

    /// <summary>A changed engine entity set applies names and parent/order state before rebuilding the hierarchy.</summary>
    [AvaloniaFact]
    public void ChangedSnapshotAddsUpdatesAndRemovesEntities()
    {
        var scene = new Scene("sync");
        var existing = new GameEntity(1, "old name");
        scene.AddEntity(existing);
        scene.AddEntity(new GameEntity(9, "removed"));
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2, 1);
        var requested = new List<int>();
        _api.OnGetEntityData = id => { requested.Add(id); return State(id, id == 2 ? 1 : 0); };
        var rebuilds = 0;
        scene.HierarchyRebuiltEvent += () => rebuilds++;

        CreateService().SynchronizeWithEngine();
        Assert.Equal(new[] { 1, 2 }, scene.Entities.Select(x => x.Id).Order());
        Assert.Same(existing, scene.GetById(1));
        Assert.Equal("engine-1", existing.Name);
        Assert.Equal("engine-2", scene.GetById(2)!.Name);
        Assert.Equal(1, scene.GetById(2)!.Transform.Parent);
        Assert.Same(scene.Hierarchy.GetNode(existing), scene.Hierarchy.GetNode(scene.GetById(2)!)!.Parent);
        Assert.Equal(new[] { 2, 1 }, requested);
        Assert.Equal(1, rebuilds);
        Assert.Empty(_api.Selections);
        Assert.Empty(_logger.Entries);
    }

    /// <summary>Removing an obsolete parent must retain a surviving child that the engine moved to the root.</summary>
    [AvaloniaFact]
    public void RemovingOldParentPreservesReparentedSurvivingChild()
    {
        var scene = new Scene("sync");
        var parent = new GameEntity(1, "removed");
        var child = new GameEntity(2, "survivor");
        scene.AddEntity(parent);
        scene.AddEntity(child);
        scene.MoveEntity(child, parent, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2);
        _api.OnGetEntityData = id => State(id, 0);
        var rebuilds = 0;
        scene.HierarchyRebuiltEvent += () => rebuilds++;

        CreateService().SynchronizeWithEngine();
        Assert.Empty(_logger.Entries);
        Assert.Same(child, Assert.Single(scene.Entities));
        Assert.Null(scene.GetById(1));
        Assert.Equal(0, child.Transform.Parent);
        Assert.NotNull(scene.Hierarchy.GetNode(child));
        Assert.Equal(1, rebuilds);
    }

    /// <summary>A surviving child can move from a removed parent to another existing surviving entity.</summary>
    [AvaloniaFact]
    public void RemovingOldParentPreservesChildReparentedToExistingEntity()
    {
        var scene = new Scene("sync");
        var oldParent = new GameEntity(1, "removed");
        var child = new GameEntity(2, "survivor");
        var newParent = new GameEntity(3, "new parent");
        var sibling = new GameEntity(4, "sibling");
        scene.AddEntity(oldParent);
        scene.AddEntity(child);
        scene.AddEntity(newParent);
        scene.AddEntity(sibling);
        scene.MoveEntity(child, oldParent, 0);
        scene.MoveEntity(sibling, newParent, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2, 3, 4);
        _api.OnGetEntityData = id => id switch
        {
            2 => State(id, newParent.Id, 1),
            4 => State(id, newParent.Id, 0),
            _ => State(id, 0)
        };

        CreateService().SynchronizeWithEngine();

        Assert.Empty(_logger.Entries);
        Assert.Equal(3, scene.Entities.Count());
        Assert.Same(child, scene.GetById(child.Id));
        Assert.Same(newParent, scene.GetById(newParent.Id));
        Assert.Same(sibling, scene.GetById(sibling.Id));
        var newParentNode = scene.Hierarchy.GetNode(newParent);
        Assert.Same(newParentNode, scene.Hierarchy.GetNode(child)!.Parent);
        Assert.Equal(1, child.Transform.Order);
        Assert.Equal(new[] { sibling, child }, newParentNode!.ChildNodes.Select(x => x.Content));
    }

    /// <summary>A surviving child can move from a removed parent to a parent added by the same snapshot.</summary>
    [AvaloniaFact]
    public void RemovingOldParentPreservesChildReparentedToNewEntity()
    {
        var scene = new Scene("sync");
        var oldParent = new GameEntity(1, "removed");
        var child = new GameEntity(2, "survivor");
        scene.AddEntity(oldParent);
        scene.AddEntity(child);
        scene.MoveEntity(child, oldParent, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2, 3);
        _api.OnGetEntityData = id => State(id, id == child.Id ? 3 : 0);

        CreateService().SynchronizeWithEngine();

        Assert.Empty(_logger.Entries);
        var newParent = scene.GetById(3);
        Assert.NotNull(newParent);
        Assert.Same(child, scene.GetById(child.Id));
        Assert.Same(scene.Hierarchy.GetNode(newParent), scene.Hierarchy.GetNode(child)!.Parent);
    }

    /// <summary>Detaching a surviving subtree boundary retains descendants while its old ancestor is removed.</summary>
    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void RemovingOldAncestorPreservesSurvivingSubtree(int obsoleteAncestorLevels)
    {
        var scene = new Scene("sync");
        var oldAncestor = new GameEntity(1, "removed");
        var subtreeRoot = new GameEntity(2, "surviving root");
        var child = new GameEntity(3, "surviving child");
        scene.AddEntity(oldAncestor);
        scene.AddEntity(subtreeRoot);
        scene.AddEntity(child);
        var oldParent = oldAncestor;
        if (obsoleteAncestorLevels == 2)
        {
            oldParent = new GameEntity(4, "removed middle");
            scene.AddEntity(oldParent);
            scene.MoveEntity(oldParent, oldAncestor, 0);
        }

        scene.MoveEntity(subtreeRoot, oldParent, 0);
        scene.MoveEntity(child, subtreeRoot, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2, 3);
        _api.OnGetEntityData = id => State(id, id == child.Id ? subtreeRoot.Id : 0);

        CreateService().SynchronizeWithEngine();

        Assert.Empty(_logger.Entries);
        Assert.Equal(2, scene.Entities.Count());
        Assert.Same(subtreeRoot, scene.GetById(subtreeRoot.Id));
        Assert.Same(child, scene.GetById(child.Id));
        Assert.Null(scene.Hierarchy.GetNode(subtreeRoot)!.Parent);
        Assert.Same(scene.Hierarchy.GetNode(subtreeRoot), scene.Hierarchy.GetNode(child)!.Parent);
    }

    /// <summary>A surviving child with unavailable state is retained and repaired to the root after its parent disappears.</summary>
    [AvaloniaFact]
    public void RemovingOldParentPreservesChildWithUnavailableState()
    {
        var scene = new Scene("sync");
        var oldParent = new GameEntity(1, "removed");
        var child = new GameEntity(2, "survivor");
        scene.AddEntity(oldParent);
        scene.AddEntity(child);
        scene.MoveEntity(child, oldParent, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(2);
        _api.OnGetEntityData = _ => null;

        CreateService().SynchronizeWithEngine();

        Assert.Empty(_logger.Entries);
        Assert.Same(child, Assert.Single(scene.Entities));
        Assert.Equal(0, child.Transform.Parent);
        Assert.Null(scene.Hierarchy.GetNode(child)!.Parent);
    }

    /// <summary>A removed parent still deletes its full subtree when none of those entities survive the snapshot.</summary>
    [AvaloniaFact]
    public void RemovingObsoleteParentDeletesObsoleteSubtree()
    {
        var scene = new Scene("sync");
        var oldParent = new GameEntity(1, "removed");
        var oldChild = new GameEntity(2, "removed child");
        scene.AddEntity(oldParent);
        scene.AddEntity(oldChild);
        scene.MoveEntity(oldChild, oldParent, 0);
        _scenes.Scene.Value = scene;
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot();
        var rebuilds = 0;
        scene.HierarchyRebuiltEvent += () => rebuilds++;

        CreateService().SynchronizeWithEngine();

        Assert.Empty(_logger.Entries);
        Assert.Empty(scene.Entities);
        Assert.Empty(scene.Hierarchy.RootNodes);
        Assert.Equal(1, rebuilds);
    }

    /// <summary>A missing scene is logged without applying a returned snapshot.</summary>
    [AvaloniaFact]
    public void MissingCurrentSceneIsReported()
    {
        _runner.Active.Value = true;
        _api.OnGetSceneEntities = () => Snapshot(1);
        CreateService().SynchronizeWithEngine();
        Assert.Contains("Current scene is missing", Assert.Single(_logger.Entries).Message);
    }

    private SceneSyncService CreateService()
    {
        var registry = new TestBehaviourRegistry();
        registry.Ids[EngineBehavioursConstants.TRANSFORM] = 10;
        var components = new TestBehaviourComponentsService
        {
            OnAdd = (entity, id) => { entity.AddBehaviour(new BehaviourComponent(id)); return true; },
            OnDelete = (entity, component) => { entity.DeleteBehaviour(component); return true; }
        };
        var applier = new EntityStateApplier(_logger, registry, components);
        var selection = new SelectionService(_api);
        return new(_importer, _runner, _api, _scenes, _logger, new EngineSelectionSyncService(selection), applier);
    }

    private static GetSceneEntitiesResponse Snapshot(params int[] ids) => new()
    {
        Entities = ids.Select(id => new GetSceneEntitiesResponse.SceneEntitiesResponseEntity { Id = id }).ToList()
    };

    private static GetEntityDataResponse State(int id, int parent, int order = 0) => new()
    {
        SceneId = id,
        Name = $"engine-{id}",
        Behaviours = new()
        {
            new() { ["REI_TYPE"] = EngineBehavioursConstants.TRANSFORM, [EngineBehavioursConstants.TRANSFORM_PARENT] = parent, [EngineBehavioursConstants.TRANSFORM_ORDER] = order }
        }
    };
}
