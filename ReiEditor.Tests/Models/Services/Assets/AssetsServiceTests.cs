using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using JsonSerializer = ReiEditor.Models.Services.Serialization.JsonSerializer;

namespace ReiEditor.Tests.Models.Services.Assets;

/// <summary>Verifies asset cache, migration, reload identity and save lifecycle within an isolated project.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "AssetLifecycle")]
public sealed class AssetsServiceTests
{
    private sealed class TestAsset : Asset, IOnDeserialized
    {
        public string Value { get; set; } = "";
        [JsonIgnore] public int DeserializationCount { get; private set; }
        public void OnDeserialized() => DeserializationCount++;
    }

    private sealed class TestContext : IDisposable
    {
        public TemporaryProjectFixture Project { get; } = new();
        public AssetRegistry Registry { get; } = new(new TestLogger<AssetRegistry>());
        public TestLogger<AssetsService> Logger { get; } = new();
        public TestAssetSerializerMigrationService Migrations { get; } = new() { OnJson = (_, json) => new(json, 0, 0, false) };
        public EditorProceduresService Procedures { get; } = new();
        public TestResourceService Resources { get; }
        public AssetsService Service { get; }

        public TestContext()
        {
            Resources = new(Project.Resources);
            var active = new ActiveProjectService(new TestLogger<ActiveProjectService>());
            active.OpenProject(Project.Project);
            Service = new(Logger, Resources, new JsonSerializer(), Migrations, active, Procedures, Registry);
        }

        public AssetInfo Info(string id, string extension = ".asset") => new(new AssetMeta(id), Project.Directory.GetPath(id + extension));
        public void Dispose() => Project.Dispose();
    }

    /// <summary>Cached assets retain identity without reading missing files; unknown IDs produce null without migration.</summary>
    [Fact]
    public async Task CacheHitAvoidsDiskAndUnknownIdsAvoidMigration()
    {
        using var context = new TestContext();
        var info = context.Info("cached");
        var cached = new TestAsset { Value = "memory" };
        context.Registry.AddToLoadedAssets(info, cached);

        Assert.Same(cached, await context.Service.Load<TestAsset>("cached"));
        Assert.Null(await context.Service.Load<TestAsset>("missing"));
        Assert.Empty(context.Migrations.JsonCalls);
        Assert.Empty(context.Resources.Writes);
        Assert.Empty(context.Logger.Entries);
    }

    /// <summary>Loading migrates source JSON before deserialization, persists changes and registers a hydrated cached asset.</summary>
    [Fact]
    public async Task LoadMigratesBeforeHydrationAndCachesResult()
    {
        using var context = new TestContext();
        var info = context.Info("legacy");
        const string SOURCE = """{"Value":"legacy"}""";
        const string MIGRATED = """{"Value":"migrated","SerializerVersion":1}""";
        await File.WriteAllTextAsync(info.FullPath, SOURCE);
        context.Registry.RegisterNewAssets(new[] { info });
        context.Migrations.OnJson = (type, json) =>
        {
            Assert.Equal(typeof(TestAsset), type);
            Assert.Equal(SOURCE, json);
            return new(MIGRATED, 0, 1, true);
        };

        var loaded = Assert.IsType<TestAsset>(await context.Service.Load<TestAsset>("legacy"));
        Assert.Equal("migrated", loaded.Value);
        Assert.Equal(1, loaded.DeserializationCount);
        Assert.Equal("legacy", loaded.AssetId);
        Assert.Equal(info.FullPath, loaded.FullPath);
        Assert.Equal(MIGRATED, await File.ReadAllTextAsync(info.FullPath));
        Assert.Equal((MIGRATED, info.FullPath), Assert.Single(context.Resources.Writes));
        Assert.Same(loaded, await context.Service.Load<TestAsset>("legacy"));
        Assert.Single(context.Migrations.JsonCalls);
        Assert.Empty(context.Logger.Entries);
    }

    /// <summary>Missing files, malformed JSON and migration exceptions return null and never populate the loaded cache.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("json")]
    [InlineData("migration")]
    public async Task LoadFailureDoesNotCachePartialAsset(string failure)
    {
        using var context = new TestContext();
        var info = context.Info("broken");
        context.Registry.RegisterNewAssets(new[] { info });
        if (failure != "missing") await File.WriteAllTextAsync(info.FullPath, failure == "json" ? "{" : "{}");
        if (failure == "migration") context.Migrations.OnJson = (_, _) => throw new InvalidOperationException("migration");

        Assert.Null(await context.Service.Load<TestAsset>(info));
        Assert.False(context.Registry.TryGetLoadedAsset("broken", out _));
        Assert.NotNull(Assert.Single(context.Logger.Entries).Exception);
        Assert.Empty(context.Resources.Writes);
    }

    /// <summary>LoadFrom resolves metadata IDs, falls back to registry paths for absent metadata and unload forces a new identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadFromResolvesMetaOrRegistryAndUnloadReleasesCache(bool withMeta)
    {
        using var context = new TestContext();
        var info = context.Info("asset");
        await File.WriteAllTextAsync(info.FullPath, """{"Value":"disk"}""");
        var lookupPath = withMeta ? context.Project.Directory.GetPath("alias.asset") : info.FullPath;
        if (withMeta) await File.WriteAllTextAsync(lookupPath + ".meta", new JsonSerializer().Serialize(info.Meta));
        context.Registry.RegisterNewAssets(new[] { info });

        var first = Assert.IsType<TestAsset>(await context.Service.LoadFrom<TestAsset>(lookupPath));
        context.Service.Unload("unknown");
        Assert.Same(first, await context.Service.Load<TestAsset>("asset"));
        context.Service.Unload("asset");
        Assert.False(context.Registry.TryGetLoadedAsset("asset", out _));
        Assert.NotSame(first, await context.Service.Load<TestAsset>("asset"));
        Assert.Equal(2, context.Migrations.JsonCalls.Count);
    }

    /// <summary>Reload updates an existing object, invokes its callback and skips ignored extensions or corrupt sibling assets.</summary>
    [Fact]
    public async Task ReloadPreservesIdentitySkipsIgnoredAndContinuesAfterFailure()
    {
        using var context = new TestContext();
        var broken = context.Info("broken");
        var good = context.Info("good");
        var ignored = context.Info("ignored", ".SCENE");
        var brokenAsset = new TestAsset { Value = "keep" };
        var goodAsset = new TestAsset { Value = "old" };
        var ignoredAsset = new TestAsset { Value = "ignored" };
        context.Registry.AddToLoadedAssets(broken, brokenAsset);
        context.Registry.AddToLoadedAssets(good, goodAsset);
        context.Registry.AddToLoadedAssets(ignored, ignoredAsset);
        await File.WriteAllTextAsync(broken.FullPath, "{");
        await File.WriteAllTextAsync(good.FullPath, """{"Value":"new"}""");

        await context.Service.ReloadLoadedAssetsFromDisk(new[] { ".scene" });
        Assert.Same(goodAsset, await context.Service.Load<TestAsset>("good"));
        Assert.Equal("new", goodAsset.Value);
        Assert.Equal(1, goodAsset.DeserializationCount);
        Assert.Equal("keep", brokenAsset.Value);
        Assert.Equal("ignored", ignoredAsset.Value);
        Assert.Equal(0, ignoredAsset.DeserializationCount);
        Assert.Equal(2, context.Migrations.JsonCalls.Count);
        Assert.Single(context.Logger.Entries);
    }

    /// <summary>Reloading a cached scene must replace its persisted entity collection rather than append duplicate IDs.</summary>
    [Fact]
    public async Task ReloadSceneReplacesEntitiesWithoutDuplicatingIds()
    {
        using var context = new TestContext();
        var info = context.Info("level", ".scene");
        var scene = new Scene("level");
        scene.AddEntity(new GameEntity(1, "before"));
        context.Registry.AddToLoadedAssets(info, scene);
        var saved = new Scene("level");
        saved.AddEntity(new GameEntity(1, "after"));
        await File.WriteAllTextAsync(info.FullPath, new JsonSerializer().Serialize(saved));

        await context.Service.ReloadLoadedAssetsFromDisk(Array.Empty<string>());

        Assert.Same(scene, await context.Service.Load<Scene>("level"));
        var entity = Assert.Single(scene.Entities);
        Assert.Equal(1, entity.Id);
        Assert.Equal("after", entity.Name);
        Assert.Same(entity, scene.GetById(1));
        Assert.NotNull(scene.Hierarchy.GetNode(entity));
        Assert.Empty(context.Logger.Entries);
    }

    /// <summary>Saving writes project and loaded assets, rejects concurrent entry and completes procedure and flags after awaiting writes.</summary>
    [Fact]
    public async Task SaveGatesReentryAndCompletesLifecycle()
    {
        using var context = new TestContext();
        var info = context.Info("asset");
        context.Registry.AddToLoadedAssets(info, new TestAsset { Value = "saved" });
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transitions = new List<bool>();
        context.Service.SaveInProcess.Subscribe(transitions.Add, invoke: false);
        context.Resources.OnWrite = (data, path) => path == context.Project.Project.ProjectFilePath ? gate.Task : context.Project.Resources.Write(data, path);
        var before = DateTime.Now;
        var pending = context.Service.SaveProject();
        try
        {
            Assert.True(context.Service.SaveInProcess.Value);
            Assert.Single(context.Procedures.ActiveProcedures);
            Assert.False(pending.IsCompleted);
            await context.Service.SaveProject();
            Assert.Single(context.Resources.Writes);
            gate.SetResult(true);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(context.Service.SaveInProcess.Value);
            Assert.Empty(context.Procedures.ActiveProcedures);
            Assert.Equal(new[] { true, false }, transitions);
            Assert.Equal("saved", JObject.Parse(await File.ReadAllTextAsync(info.FullPath))["Value"]!.Value<string>());
            Assert.Equal(context.Project.Project.ProjectFilePath, context.Resources.Writes[0].Path);
            Assert.InRange(context.Project.Project.LastEditTime, before, DateTime.Now);
        }
        finally
        {
            gate.TrySetResult(true);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>A failed project or asset write releases saving state, completes its procedure and allows a subsequent save.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveExceptionsReleaseStateAndAllowRetry(bool failProject)
    {
        using var context = new TestContext();
        var broken = context.Info("broken");
        var good = context.Info("good");
        context.Registry.AddToLoadedAssets(broken, new TestAsset { Value = "bad" });
        context.Registry.AddToLoadedAssets(good, new TestAsset { Value = "good" });
        context.Resources.OnWrite = (data, path) => path == (failProject ? context.Project.Project.ProjectFilePath : broken.FullPath)
            ? Task.FromException<bool>(new IOException("write")) : context.Project.Resources.Write(data, path);

        await context.Service.SaveProject();
        Assert.False(context.Service.SaveInProcess.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Single(context.Logger.Entries, x => x.Exception != null);
        if (!failProject) Assert.True(File.Exists(good.FullPath));
        else Assert.False(File.Exists(good.FullPath));
        context.Resources.OnWrite = null;
        await context.Service.SaveProject();
        Assert.True(File.Exists(broken.FullPath));
        Assert.True(File.Exists(good.FullPath));
        Assert.False(context.Service.SaveInProcess.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }
}
