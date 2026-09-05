using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>
/// Verifies current scene entities forwarded to engine state synchronization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Scenes")]
public sealed class SceneStateSynchronizerTests
{
    /// <summary>
    /// Records entities requested for state update.
    /// </summary>
    private sealed class TestEntitySyncService : IEntitySyncService
    {
        public List<GameEntity> UpdatedEntities { get; } = new();

        /// <summary>
        /// Records one entity update request.
        /// </summary>
        public void UpdateEntityState(GameEntity entity) => UpdatedEntities.Add(entity);
    }

    /// <summary>
    /// Synchronization without current scene does not request entity updates.
    /// </summary>
    [Fact]
    public void TestSynchronizeStateWithEngineDoesNothingWithoutCurrentScene()
    {
        var sceneManagement = new TestSceneManagementService();
        var entitySync = new TestEntitySyncService();
        var synchronizer = new SceneStateSynchronizer(sceneManagement, entitySync);

        synchronizer.SynchronizeStateWithEngine();

        Assert.Empty(entitySync.UpdatedEntities);
    }

    /// <summary>
    /// Synchronization updates every current scene entity once in scene order.
    /// </summary>
    [Fact]
    public void TestSynchronizeStateWithEngineUpdatesEveryCurrentSceneEntity()
    {
        var scene = new Scene("Scene");
        var first = new GameEntity(1, "First");
        var second = new GameEntity(2, "Second");
        scene.AddEntity(first);
        scene.AddEntity(second);
        var sceneManagement = new TestSceneManagementService();
        sceneManagement.Scene.Value = scene;
        var entitySync = new TestEntitySyncService();
        var synchronizer = new SceneStateSynchronizer(sceneManagement, entitySync);

        synchronizer.SynchronizeStateWithEngine();

        Assert.Equal(new[] { first, second }, entitySync.UpdatedEntities);
    }

    /// <summary>
    /// Synchronization reads current scene at call time after scene switches.
    /// </summary>
    [Fact]
    public void TestSynchronizeStateWithEngineUsesLatestCurrentScene()
    {
        var firstScene = new Scene("First");
        var firstEntity = new GameEntity(1, "First entity");
        firstScene.AddEntity(firstEntity);
        var secondScene = new Scene("Second");
        var secondEntity = new GameEntity(2, "Second entity");
        secondScene.AddEntity(secondEntity);
        var sceneManagement = new TestSceneManagementService();
        var entitySync = new TestEntitySyncService();
        var synchronizer = new SceneStateSynchronizer(sceneManagement, entitySync);

        sceneManagement.Scene.Value = firstScene;
        synchronizer.SynchronizeStateWithEngine();
        sceneManagement.Scene.Value = secondScene;
        synchronizer.SynchronizeStateWithEngine();

        Assert.Equal(new[] { firstEntity, secondEntity }, entitySync.UpdatedEntities);
    }
}
