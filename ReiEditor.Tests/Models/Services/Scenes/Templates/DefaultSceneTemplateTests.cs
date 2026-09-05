using System.Numerics;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes.Templates;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes.Templates;

/// <summary>
/// Verifies default scene camera and light creation plus initial component values.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Scenes")]
public sealed class DefaultSceneTemplateTests
{
    private const int TRANSFORM_ID = 1;
    private const int CAMERA_ID = 2;
    private const int AMBIENT_LIGHT_ID = 3;
    private const int POINT_LIGHT_ID = 4;

    /// <summary>
    /// Successful setup creates camera and point light, adds required behaviours, and initializes camera values.
    /// </summary>
    [Fact]
    public async Task TestSetupSceneCreatesDefaultEntitiesComponentsAndValues()
    {
        var mainCamera = CreateCameraEntity();
        var pointLight = new GameEntity(2, "Point Light");
        var entityService = CreateEntityService(mainCamera, pointLight);
        var template = new DefaultSceneTemplate(entityService, CreateRegistry());

        await template.SetupScene();

        Assert.Equal(new[] { "Main Camera", "Point Light" }, entityService.CreateCalls.Select(x => x.Name));
        Assert.All(entityService.CreateCalls, call => Assert.Null(call.Parent));
        Assert.Equal(
            new[]
            {
                (mainCamera, CAMERA_ID),
                (mainCamera, AMBIENT_LIGHT_ID),
                (pointLight, POINT_LIGHT_ID)
            },
            entityService.AddBehaviourCalls);
        AssertVector(mainCamera.GetBehaviour(TRANSFORM_ID)!, EngineBehavioursConstants.TRANSFORM_POSITION, 0, 2, -10);
        AssertVector(mainCamera.GetBehaviour(TRANSFORM_ID)!, EngineBehavioursConstants.TRANSFORM_ROTATION, 0, -15, 0);
        AssertColor(mainCamera.GetBehaviour(CAMERA_ID)!, 0.074, 0.090, 0.116, 1);
        Assert.True(mainCamera.HasComponent(AMBIENT_LIGHT_ID));
        Assert.True(pointLight.HasComponent(POINT_LIGHT_ID));
    }

    /// <summary>
    /// Null camera creation does not prevent point light creation and configuration.
    /// </summary>
    [Fact]
    public async Task TestSetupSceneContinuesWithPointLightWhenCameraCreationReturnsNull()
    {
        var pointLight = new GameEntity(2, "Point Light");
        var entityService = CreateEntityService(null, pointLight);
        var template = new DefaultSceneTemplate(entityService, CreateRegistry());

        await template.SetupScene();

        Assert.Equal(new[] { "Main Camera", "Point Light" }, entityService.CreateCalls.Select(x => x.Name));
        var addCall = Assert.Single(entityService.AddBehaviourCalls);
        Assert.Same(pointLight, addCall.Entity);
        Assert.Equal(POINT_LIGHT_ID, addCall.BehaviourId);
        Assert.True(pointLight.HasComponent(POINT_LIGHT_ID));
    }

    /// <summary>
    /// Null point light creation leaves configured camera intact and skips point-light behaviour addition.
    /// </summary>
    [Fact]
    public async Task TestSetupSceneSkipsPointLightBehaviourWhenLightCreationReturnsNull()
    {
        var mainCamera = CreateCameraEntity();
        var entityService = CreateEntityService(mainCamera, null);
        var template = new DefaultSceneTemplate(entityService, CreateRegistry());

        await template.SetupScene();

        Assert.Equal(new[] { CAMERA_ID, AMBIENT_LIGHT_ID }, entityService.AddBehaviourCalls.Select(x => x.BehaviourId));
        AssertVector(mainCamera.GetBehaviour(TRANSFORM_ID)!, EngineBehavioursConstants.TRANSFORM_POSITION, 0, 2, -10);
        AssertColor(mainCamera.GetBehaviour(CAMERA_ID)!, 0.074, 0.090, 0.116, 1);
    }

    /// <summary>
    /// Missing required camera registry entry surfaces configuration failure before point-light creation.
    /// </summary>
    [Fact]
    public async Task TestSetupSceneThrowsWhenRequiredBehaviourIdIsMissing()
    {
        var mainCamera = CreateCameraEntity();
        var entityService = CreateEntityService(mainCamera, new GameEntity(2, "Point Light"));
        var registry = CreateRegistry();
        registry.Ids.Remove(EngineBehavioursConstants.CAMERA);
        var template = new DefaultSceneTemplate(entityService, registry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => template.SetupScene());

        Assert.Equal(new[] { "Main Camera" }, entityService.CreateCalls.Select(x => x.Name));
        Assert.Empty(entityService.AddBehaviourCalls);
    }

    /// <summary>
    /// Creates registry containing IDs required by default template.
    /// </summary>
    private static TestBehaviourRegistry CreateRegistry()
    {
        var registry = new TestBehaviourRegistry();
        registry.Ids[EngineBehavioursConstants.TRANSFORM] = TRANSFORM_ID;
        registry.Ids[EngineBehavioursConstants.CAMERA] = CAMERA_ID;
        registry.Ids[EngineBehavioursConstants.AMBIENT_LIGHT] = AMBIENT_LIGHT_ID;
        registry.Ids[EngineBehavioursConstants.POINT_LIGHT] = POINT_LIGHT_ID;
        return registry;
    }

    /// <summary>
    /// Creates camera entity with hydrated transform expected before template adds camera behaviours.
    /// </summary>
    private static GameEntity CreateCameraEntity()
    {
        var entity = new GameEntity(1, "Main Camera");
        entity.AddBehaviour(SceneComponentBuilder.Transform(TRANSFORM_ID, Vector3.Zero, Vector3.Zero));
        return entity;
    }

    /// <summary>
    /// Creates recording entity service with queued entity results and behaviour hydration.
    /// </summary>
    private static TestEntityManagementService CreateEntityService(GameEntity? camera, GameEntity? light)
    {
        var responses = new Queue<GameEntity?>(new[] { camera, light });
        return new TestEntityManagementService
        {
            OnCreate = (_, _) => Task.FromResult(responses.Dequeue()),
            OnAddBehaviour = (entity, behaviourId) => entity.AddBehaviour(CreateBehaviour(behaviourId))
        };
    }

    /// <summary>
    /// Creates hydrated behaviour required by template after add operation.
    /// </summary>
    private static BehaviourComponent CreateBehaviour(int behaviourId)
    {
        var behaviour = new BehaviourComponent(behaviourId);
        if (behaviourId != CAMERA_ID) return behaviour;

        behaviour.AddProperty(SceneComponentBuilder.Vector(
            EngineBehavioursConstants.CAMERA_BACKGROUND_COLOR,
            ("r", 0),
            ("g", 0),
            ("b", 0),
            ("a", 0)));
        return behaviour;
    }

    /// <summary>
    /// Verifies named vector component values as numeric values.
    /// </summary>
    private static void AssertVector(BehaviourComponent component, string propertyName, double x, double y, double z)
    {
        var values = Assert.IsType<Dictionary<string, SerializedProperty>>(component.GetProperty(propertyName).Value);
        Assert.Equal(x, Convert.ToDouble(values["x"].Value));
        Assert.Equal(y, Convert.ToDouble(values["y"].Value));
        Assert.Equal(z, Convert.ToDouble(values["z"].Value));
    }

    /// <summary>
    /// Verifies default camera background color channels.
    /// </summary>
    private static void AssertColor(BehaviourComponent component, double r, double g, double b, double a)
    {
        var values = Assert.IsType<Dictionary<string, SerializedProperty>>(
            component.GetProperty(EngineBehavioursConstants.CAMERA_BACKGROUND_COLOR).Value);
        Assert.Equal(r, Convert.ToDouble(values["r"].Value));
        Assert.Equal(g, Convert.ToDouble(values["g"].Value));
        Assert.Equal(b, Convert.ToDouble(values["b"].Value));
        Assert.Equal(a, Convert.ToDouble(values["a"].Value));
    }
}
