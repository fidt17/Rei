using System.Numerics;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies camera-relative placement, spacing and fallback positions without engine state.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneAssetDrop")]
public sealed class SceneAssetPlacementServiceTests
{
    /// <summary>Missing scene, behaviour IDs, camera, transform or vector data all use the fallback snapshot.</summary>
    [Theory]
    [InlineData("scene")]
    [InlineData("ids")]
    [InlineData("camera")]
    [InlineData("transform")]
    [InlineData("vectors")]
    public void MissingCameraDataUsesFallback(string missing)
    {
        var scenes = new TestSceneManagementService();
        var registry = new TestBehaviourRegistry();
        var logger = new TestLogger<SceneAssetPlacementService>();
        if (missing != "scene") scenes.Scene.Value = new Scene("drop");
        if (missing != "ids")
        {
            registry.Ids[EngineBehavioursConstants.CAMERA] = 1;
            registry.Ids[EngineBehavioursConstants.TRANSFORM] = 2;
        }
        if (missing is "transform" or "vectors")
        {
            var camera = new GameEntity(1, "camera");
            camera.AddBehaviour(new BehaviourComponent(1));
            if (missing == "vectors") camera.AddBehaviour(new BehaviourComponent(2));
            scenes.Scene.Value!.AddEntity(camera);
        }

        var result = Assert.Single(new SceneAssetPlacementService(logger, scenes, registry).BuildPlacements(new[] { Target(AssetType.Texture) }));
        Assert.Equal(new Vector3(0, 0, 10), result.Position);
        Assert.Equal(Vector3.Zero, result.Rotation);
        Assert.Single(logger.Entries);
    }

    /// <summary>Rotated camera places two assets symmetrically and copies rotation only to textures.</summary>
    [Fact]
    public void CameraPlacementUsesForwardAndSymmetricRightOffsets()
    {
        var scene = new Scene("drop");
        var camera = new GameEntity(1, "camera");
        camera.AddBehaviour(new BehaviourComponent(1));
        camera.AddBehaviour(SceneComponentBuilder.Transform(2, new(3, 4, 5), new(0, 90, 15)));
        scene.AddEntity(camera);
        var scenes = new TestSceneManagementService();
        scenes.Scene.Value = scene;
        var registry = new TestBehaviourRegistry();
        registry.Ids[EngineBehavioursConstants.CAMERA] = 1;
        registry.Ids[EngineBehavioursConstants.TRANSFORM] = 2;
        var result = new SceneAssetPlacementService(new TestLogger<SceneAssetPlacementService>(), scenes, registry)
            .BuildPlacements(new[] { Target(AssetType.Texture), Target(AssetType.Model) });

        Assert.Equal(2, result.Count);
        AssertVector(new(13, 4, 6), result[0].Position);
        AssertVector(new(13, 4, 4), result[1].Position);
        Assert.Equal(new Vector3(0, 90, 15), result[0].Rotation);
        Assert.Equal(Vector3.Zero, result[1].Rotation);
    }

    /// <summary>An empty batch has no camera work, while three fallback assets retain centered two-unit spacing.</summary>
    [Fact]
    public void EmptyAndOddBatchesHaveStableSpacing()
    {
        var logger = new TestLogger<SceneAssetPlacementService>();
        var service = new SceneAssetPlacementService(logger, new TestSceneManagementService(), new TestBehaviourRegistry());
        Assert.Empty(service.BuildPlacements(Array.Empty<SceneAssetDropTarget>()));
        Assert.Empty(logger.Entries);
        var result = service.BuildPlacements(Enumerable.Repeat(Target(AssetType.Model), 3).ToArray());
        Assert.Equal(new[] { new Vector3(-2, 0, 10), new Vector3(0, 0, 10), new Vector3(2, 0, 10) }, result.Select(x => x.Position));
        Assert.Single(logger.Entries);
    }

    private static SceneAssetDropTarget Target(AssetType type) => new("asset", type, "id", "name");
    private static void AssertVector(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 0.0001f, $"Expected {expected}; actual {actual}");
}
