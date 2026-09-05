using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Creation;

/// <summary>Verifies asset file and metadata creation, stable IDs, cache registration and failed-write boundaries.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "AssetCreation")]
public sealed class AssetCreatorTests
{
    private sealed class TestContext : IDisposable
    {
        public TemporaryProjectFixture Project { get; } = new();
        public TestResourceService Resources { get; }
        public AssetRegistry Registry { get; } = new(new TestLogger<AssetRegistry>());
        public TestLogger<AssetCreator> Logger { get; } = new();
        public AssetCreator Creator { get; }
        public TestContext()
        {
            Resources = new(Project.Resources);
            var serializer = new JsonSerializer();
            var meta = new MetaFilesService(Resources, serializer, new TestLogger<MetaFilesService>());
            Creator = new(Resources, serializer, Logger, Registry, meta);
        }
        public void Dispose() => Project.Dispose();
    }

    /// <summary>Successful creation writes asset before meta and publishes the same fully identified instance in the cache.</summary>
    [Fact]
    public async Task CreateWritesContentAndMetaBeforePublishingCache()
    {
        using var context = new TestContext();
        var scene = new Scene("Created");
        var path = context.Resources.GetProjectPath("Assets/Created.scene");
        context.Resources.OnWrite = (data, destination) =>
        {
            Assert.False(context.Registry.TryGetLoadedAsset("explicit-id", out _));
            if (destination.EndsWith(".meta", StringComparison.Ordinal)) Assert.True(File.Exists(path));
            return context.Project.Resources.Write(data, destination);
        };

        Assert.True(await context.Creator.Create(scene, "explicit-id", "Assets/Created.scene"));
        Assert.Equal(new[] { path, path + ".meta" }, context.Resources.Writes.Select(x => x.Path));
        Assert.Equal("Created", JObject.Parse(await File.ReadAllTextAsync(path))["Name"]!.Value<string>());
        Assert.Equal(1, JObject.Parse(await File.ReadAllTextAsync(path))["SerializerVersion"]!.Value<int>());
        Assert.Equal("explicit-id", (await context.Resources.Load<AssetMeta>(path + ".meta")).AssetId);
        Assert.True(context.Registry.TryGetLoadedAsset("explicit-id", out var loaded));
        Assert.Same(scene, loaded);
        Assert.Equal(path, scene.FullPath);
        Assert.Equal("explicit-id", scene.AssetId);
        Assert.Empty(context.Logger.Entries);
    }

    /// <summary>Engine resource IDs derive from normalized filenames while ordinary generated IDs are nonempty distinct GUIDs.</summary>
    [Fact]
    public async Task EngineResourcesUseStableIdsAndOrdinaryIdsAreUnique()
    {
        using var context = new TestContext();
        var scene = new Scene("builtin");
        Assert.True(await context.Creator.Create(scene, "Engine Resources/Scenes/Default.SCENE"));
        Assert.Equal("rei_default.scene", scene.AssetId);
        var first = context.Creator.AllocateAssetId();
        var second = context.Creator.AllocateAssetId();
        Assert.True(Guid.TryParse(first, out _));
        Assert.True(Guid.TryParse(second, out _));
        Assert.NotEqual(first, second);
    }

    /// <summary>An existing asset rejects creation without overwriting its content, metadata or registry state.</summary>
    [Fact]
    public async Task ExistingFileIsNotOverwritten()
    {
        using var context = new TestContext();
        var path = context.Project.Directory.GetPath("existing.scene");
        await File.WriteAllTextAsync(path, "sentinel");
        Assert.False(await context.Creator.Create(new Scene("new"), "new-id", path));
        Assert.Equal("sentinel", await File.ReadAllTextAsync(path));
        Assert.False(File.Exists(path + ".meta"));
        Assert.Empty(context.Resources.Writes);
        Assert.Empty(context.Registry.GetAllAssets());
        Assert.NotNull(Assert.Single(context.Logger.Entries).Exception);
    }

    /// <summary>Asset or metadata write failure returns false and never publishes the incomplete asset into the cache.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteFailureDoesNotPublishIncompleteAsset(bool failMeta)
    {
        using var context = new TestContext();
        var path = context.Resources.GetProjectPath("Assets/failure.scene");
        context.Resources.OnWrite = (data, destination) => destination == (failMeta ? path + ".meta" : path)
            ? Task.FromResult(false) : context.Project.Resources.Write(data, destination);
        Assert.False(await context.Creator.Create(new Scene("failure"), "id", path));
        Assert.False(context.Registry.TryGetLoadedAsset("id", out _));
        Assert.False(context.Registry.TryGetById("id", out _));
        Assert.False(File.Exists(path + ".meta"));
        Assert.Equal(failMeta ? 2 : 1, context.Resources.Writes.Count);
        Assert.NotNull(Assert.Single(context.Logger.Entries).Exception);
    }

    /// <summary>A path without an asset extension must fail validation before producing files or registry entries.</summary>
    [Fact]
    public async Task MissingExtensionIsRejectedBeforeWriting()
    {
        using var context = new TestContext();
        var path = context.Resources.GetProjectPath("Assets/Untyped");
        Assert.False(await context.Creator.Create(new Scene("untyped"), "id", path));
        Assert.Empty(context.Resources.Writes);
        Assert.False(File.Exists(path));
        Assert.Empty(context.Registry.GetAllAssets());
    }
}
