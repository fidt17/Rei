using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies supported asset targets, stable IDs and collision-free batch names using an isolated project.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "SceneAssetDrop")]
public sealed class SceneAssetDropTargetBuilderServiceTests
{
    /// <summary>Targets retain input order, deduplicate paths case-insensitively and avoid scene and batch name collisions.</summary>
    [Fact]
    public void TargetsResolveIdsAndDeduplicatePathsAndNames()
    {
        using var project = new TemporaryProjectFixture();
        var model = project.Directory.GetPath("Assets/tree.OBJ");
        var texture = project.Directory.GetPath("Assets/tree.png");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta("model"), model), new AssetInfo(new AssetMeta("texture"), texture) });
        var scenes = new TestSceneManagementService();
        scenes.Scene.Value = new Scene("drop");
        scenes.Scene.Value.AddEntity(new GameEntity(1, "tree"));
        var service = new SceneAssetDropTargetBuilderService(new TestLogger<SceneAssetDropTargetBuilderService>(), new AssetTypeMapper(), registry, project.Resources, scenes);

        var result = service.BuildTargets(new[] { " ", model, model.ToUpperInvariant(), texture, project.Directory.GetPath("Assets/ignore.mat") });
        Assert.Equal(new[] { "model", "texture" }, result.Select(x => x.AssetId));
        Assert.Equal(new[] { "tree 1", "tree 2" }, result.Select(x => x.EntityName));
        Assert.Equal(new[] { AssetType.Model, AssetType.Texture }, result.Select(x => x.AssetType));
        Assert.Equal(new[] { model, texture }, result.Select(x => x.AssetPath));
        Assert.True(service.CanHandleAssetPaths(new[] { texture }));
        Assert.False(service.CanHandleAssetPaths(Array.Empty<string>()));
    }

    /// <summary>Engine resources get generated IDs; unresolved external assets and registered blank IDs are rejected.</summary>
    [Fact]
    public void EngineResourcesResolveWhileUnknownAndBlankIdsAreRejected()
    {
        using var project = new TemporaryProjectFixture();
        var builtin = project.Resources.GetProjectPath("Engine Resources/Textures/White.PNG");
        var blank = project.Directory.GetPath("blank.obj");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta(""), blank) });
        var logger = new TestLogger<SceneAssetDropTargetBuilderService>();
        var service = new SceneAssetDropTargetBuilderService(logger, new AssetTypeMapper(), registry, project.Resources, new TestSceneManagementService());

        var result = Assert.Single(service.BuildTargets(new[] { project.Directory.GetPath("unknown.obj"), builtin, blank }));
        Assert.Equal("rei_white.png", result.AssetId);
        Assert.Equal("White", result.EntityName);
        Assert.Single(logger.Entries);
    }
}
