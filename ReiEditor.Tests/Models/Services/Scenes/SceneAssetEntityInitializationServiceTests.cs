using System.Numerics;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies dropped asset creation, renderer selection, transform payloads and reported failures.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneAssetDrop")]
public sealed class SceneAssetEntityInitializationServiceTests
{
    private sealed class TestWriter : IEntityDataWriterService
    {
        public List<(GameEntity Entity, int Behaviour, string Property, IReadOnlyDictionary<string, object?> Value)> Calls { get; } = new();
        public Func<string, bool> OnWrite { get; set; } = _ => true;
        public bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, object? value) => throw new NotSupportedException();
        public bool SetBehaviourProperty(GameEntity entity, int behaviourId, string propertyName, IReadOnlyDictionary<string, object?> value)
        {
            Calls.Add((entity, behaviourId, propertyName, value));
            return OnWrite(propertyName);
        }
    }

    /// <summary>Models and textures select the appropriate renderer and write independent position, rotation and asset payloads.</summary>
    [Theory]
    [InlineData(AssetType.Model)]
    [InlineData(AssetType.Texture)]
    public async Task SupportedAssetsInitializeRendererAndProperties(AssetType type)
    {
        var entity = new GameEntity(7, "created");
        var management = new TestEntityManagementService { OnCreate = (_, _) => Task.FromResult<GameEntity?>(entity), OnAddBehaviour = (_, _) => { } };
        var writer = new TestWriter();
        var logger = new TestLogger<SceneAssetEntityInitializationService>();
        var service = new SceneAssetEntityInitializationService(logger, management, writer, Registry());
        var placement = new SceneAssetDropPlacement(new(1, 2, 3), new(4, 5, 6));

        Assert.True(await service.CreateEntityForAsset(new("asset", type, "asset-id", "new entity"), placement));
        Assert.Equal(("new entity", (GameEntity?)null), Assert.Single(management.CreateCalls));
        Assert.Equal((entity, type == AssetType.Model ? 2 : 3), Assert.Single(management.AddBehaviourCalls));
        Assert.Equal(3, writer.Calls.Count);
        Assert.All(writer.Calls, call => Assert.Same(entity, call.Entity));
        Assert.Equal(1, writer.Calls[0].Behaviour);
        Assert.Equal(EngineBehavioursConstants.TRANSFORM_POSITION, writer.Calls[0].Property);
        AssertVectorPayload(writer.Calls[0].Value, new(1, 2, 3));
        Assert.Equal(1, writer.Calls[1].Behaviour);
        Assert.Equal(EngineBehavioursConstants.TRANSFORM_ROTATION, writer.Calls[1].Property);
        AssertVectorPayload(writer.Calls[1].Value, new(4, 5, 6));
        Assert.Equal(type == AssetType.Model ? 2 : 3, writer.Calls[2].Behaviour);
        Assert.Equal(type == AssetType.Model ? EngineBehavioursConstants.MESH_RENDERER_MODEL : EngineBehavioursConstants.SPRITE_RENDERER_SPRITE, writer.Calls[2].Property);
        Assert.Equal("asset-id", Assert.Single(writer.Calls[2].Value).Value);
        Assert.Equal(EngineBehavioursConstants.ASSET_REF_ID, Assert.Single(writer.Calls[2].Value).Key);
        Assert.Empty(logger.Entries);
    }

    /// <summary>Missing entity or renderer, failed asset assignment and dependency exceptions return false without pretending success.</summary>
    [Theory]
    [InlineData("entity")]
    [InlineData("renderer")]
    [InlineData("asset-write")]
    [InlineData("exception")]
    public async Task CreationFailuresReturnFalse(string failure)
    {
        var entity = new GameEntity(1, "created");
        var management = new TestEntityManagementService
        {
            OnCreate = (_, _) => failure == "exception" ? Task.FromException<GameEntity?>(new InvalidOperationException("create")) : Task.FromResult(failure == "entity" ? null : entity),
            OnAddBehaviour = (_, _) => { }
        };
        var registry = Registry();
        if (failure == "renderer") registry.Ids.Remove(EngineBehavioursConstants.MESH_RENDERER);
        var writer = new TestWriter { OnWrite = name => failure != "asset-write" || name != EngineBehavioursConstants.MESH_RENDERER_MODEL };
        var logger = new TestLogger<SceneAssetEntityInitializationService>();
        var service = new SceneAssetEntityInitializationService(logger, management, writer, registry);

        Assert.False(await service.CreateEntityForAsset(new("asset", AssetType.Model, "id", "name"), new(Vector3.Zero, Vector3.Zero)));
        if (failure is "entity" or "renderer" or "exception")
        {
            Assert.Empty(management.AddBehaviourCalls);
            Assert.Empty(writer.Calls);
        }
        else Assert.Equal(3, writer.Calls.Count);
        if (failure != "entity") Assert.Single(logger.Entries);
        if (failure == "exception") Assert.IsType<InvalidOperationException>(Assert.Single(logger.Entries).Exception);
    }

    private static TestBehaviourRegistry Registry()
    {
        var registry = new TestBehaviourRegistry();
        registry.Ids[EngineBehavioursConstants.TRANSFORM] = 1;
        registry.Ids[EngineBehavioursConstants.MESH_RENDERER] = 2;
        registry.Ids[EngineBehavioursConstants.SPRITE_RENDERER] = 3;
        return registry;
    }

    private static void AssertVectorPayload(IReadOnlyDictionary<string, object?> payload, Vector3 expected)
    {
        Assert.Equal(3, payload.Count);
        Assert.Equal(expected.X, payload["x"]);
        Assert.Equal(expected.Y, payload["y"]);
        Assert.Equal(expected.Z, payload["z"]);
    }
}
