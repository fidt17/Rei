using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Import;

/// <summary>Verifies full and partial import, metadata repair, refresh routing and import lifecycle in isolated projects.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "AssetImport")]
public sealed class AssetImporterTests
{
    private sealed class TestBehaviours : IBehaviourRegistry
    {
        public Func<Task> OnRefresh { get; set; } = () => Task.CompletedTask;
        public int RefreshCount { get; private set; }
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => throw new NotSupportedException();
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => throw new NotSupportedException();
        public int? GetIdByName(string name) => throw new NotSupportedException();
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() { RefreshCount++; return OnRefresh(); }
    }

    private sealed class TestShaders : IShaderRegistry
    {
        public Func<Task> OnRefresh { get; set; } = () => Task.CompletedTask;
        public int RefreshCount { get; private set; }
        public IReadOnlyDictionary<string, Shader> Shaders => throw new NotSupportedException();
        public bool TryGetById(string assetId, [NotNullWhen(true)] out Shader? shader) => throw new NotSupportedException();
        public Task RefreshShaders() { RefreshCount++; return OnRefresh(); }
    }

    private sealed class TestBehaviourFiles : IBehaviourFileUtility
    {
        public Func<string, bool> OnIsBehaviour { get; set; } = _ => false;
        public List<BehaviourFileUtility.BehaviourPathData> GetAllBehaviours() => throw new NotSupportedException();
        public Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas() => throw new NotSupportedException();
        public bool TryGetBehaviourNameFrom(string text, out string name) => throw new NotSupportedException();
        public Task<bool> IsBehaviourFile(string path) => Task.FromResult(OnIsBehaviour(path));
    }

    private sealed class TestAssets : IAssetsService
    {
        public Dictionary<string, Scene> Scenes { get; } = new();
        public List<string> LoadedPaths { get; } = new();
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => throw new NotSupportedException();
        public Task<T?> Load<T>(string assetId) where T : Asset => throw new NotSupportedException();
        public Task<T?> Load<T>(AssetInfo assetInfo) where T : Asset => throw new NotSupportedException();
        public Task<T?> LoadFrom<T>(string projectPath) where T : Asset
        {
            LoadedPaths.Add(projectPath);
            return Task.FromResult(Scenes.TryGetValue(projectPath, out var scene) ? (T?)(Asset)scene : null);
        }
        public void Unload(string assetId) => throw new NotSupportedException();
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions) => throw new NotSupportedException();
        public Task SaveProject() => throw new NotSupportedException();
    }

    private sealed class TestContext : IDisposable
    {
        public TemporaryProjectFixture Project { get; } = new();
        public TestResourceService Resources { get; }
        public AssetRegistry Registry { get; } = new(new TestLogger<AssetRegistry>());
        public TestLogger<AssetImporter> Logger { get; } = new();
        public TestAssetSerializerMigrationService Migrations { get; } = new() { OnFile = _ => Task.FromResult(false) };
        public TestBehaviours Behaviours { get; } = new();
        public TestShaders Shaders { get; } = new();
        public TestBehaviourFiles BehaviourFiles { get; } = new();
        public TestAssets Assets { get; } = new();
        public TestBehaviourComponentsService Components { get; } = new();
        public EditorProceduresService Procedures { get; } = new();
        public AssetImporter Importer { get; }

        public TestContext()
        {
            Resources = new(Project.Resources);
            Directory.CreateDirectory(Resources.GetProjectPath());
            var serializer = new JsonSerializer();
            var meta = new MetaFilesService(Resources, serializer, new TestLogger<MetaFilesService>());
            var creator = new AssetCreator(Resources, serializer, new TestLogger<AssetCreator>(), Registry, meta);
            Importer = new(Logger, Resources, creator, meta, Behaviours, Shaders, Registry, Components, BehaviourFiles, serializer, Migrations, Assets, Procedures);
        }

        public async Task<string> File(string name, string data = "asset")
        {
            var path = Resources.GetProjectPath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await System.IO.File.WriteAllTextAsync(path, data);
            return path;
        }

        public void Dispose() => Project.Dispose();
    }

    /// <summary>Full import replaces stale registry entries, creates metadata and refreshes registries after registering assets.</summary>
    [Fact]
    public async Task FullImportReplacesRegistryAndCompletesBeforeNotification()
    {
        using var context = new TestContext();
        var file = await context.File("Assets/image.png");
        await context.File("Scripts/ignored.cpp");
        await context.File("project.vcxproj");
        var stale = new AssetInfo(new AssetMeta("stale"), context.Resources.GetProjectPath("removed.png"));
        context.Registry.RegisterNewAssets(new[] { stale });
        var calls = new List<string>();
        context.Behaviours.OnRefresh = () =>
        {
            Assert.True(context.Registry.TryGetByPath(file, out _));
            calls.Add("behaviours");
            return Task.CompletedTask;
        };
        context.Shaders.OnRefresh = () => { calls.Add("shaders"); return Task.CompletedTask; };
        context.Importer.ImportedAssetsEvent += () =>
        {
            Assert.False(context.Importer.IsImporting.Value);
            Assert.Empty(context.Procedures.ActiveProcedures);
            calls.Add("finished");
        };

        var imported = Assert.Single(await context.Importer.ReimportAll());
        Assert.Equal(file, imported.FullPath);
        Assert.True(Guid.TryParse(imported.Meta.AssetId, out _));
        Assert.Equal(imported.Meta.AssetId, (await context.Resources.Load<AssetMeta>(file + ".meta")).AssetId);
        Assert.False(context.Registry.TryGetById("stale", out _));
        Assert.Equal(new[] { file }, context.Migrations.FileCalls);
        Assert.Equal(new[] { "behaviours", "shaders", "finished" }, calls);
        Assert.DoesNotContain(context.Logger.Entries, x => x.Exception != null);
    }

    /// <summary>Partial import repairs corrupt metadata, keeps unrelated registry entries and retains IDs on repeated import.</summary>
    [Fact]
    public async Task PartialImportRepairsMetadataAndPreservesExistingAssets()
    {
        using var context = new TestContext();
        var path = await context.File("Assets/image.png");
        await File.WriteAllTextAsync(path + ".meta", "{");
        var unrelated = new AssetInfo(new AssetMeta("unrelated"), context.Resources.GetProjectPath("other.png"));
        context.Registry.RegisterNewAssets(new[] { unrelated });

        var first = Assert.Single(await context.Importer.ReimportPaths(new[] { path, path }));
        var persisted = await context.Resources.Load<AssetMeta>(path + ".meta");
        var second = Assert.Single(await context.Importer.ReimportPaths(new[] { path }));
        Assert.Equal(first.Meta.AssetId, persisted.AssetId);
        Assert.Equal(first.Meta.AssetId, second.Meta.AssetId);
        Assert.True(context.Registry.TryGetById("unrelated", out var kept));
        Assert.Same(unrelated, kept);
        Assert.Equal(0, context.Behaviours.RefreshCount);
        Assert.Equal(0, context.Shaders.RefreshCount);
    }

    /// <summary>Engine assets replace arbitrary IDs with stable IDs while preserving additional metadata.</summary>
    [Fact]
    public async Task EngineResourceImportNormalizesIdsWithoutDroppingMetadata()
    {
        using var context = new TestContext();
        var path = await context.File("Engine Resources/Textures/White.PNG");
        var meta = new AssetMeta("old-id");
        meta.AddData("tag", "preserved");
        await File.WriteAllTextAsync(path + ".meta", new JsonSerializer().Serialize(meta));

        var imported = Assert.Single(await context.Importer.ReimportPaths(new[] { path }));
        Assert.Equal("rei_white.png", imported.Meta.AssetId);
        Assert.Equal("preserved", imported.Meta.GetData<string>("tag"));
        var saved = await context.Resources.Load<AssetMeta>(path + ".meta");
        Assert.Equal("rei_white.png", saved.AssetId);
        Assert.Equal("preserved", saved.GetData<string>("tag"));
    }

    /// <summary>A failed metadata write skips only its asset and leaves a later valid asset importable.</summary>
    [Fact]
    public async Task PartialImportContinuesAfterIndividualFailure()
    {
        using var context = new TestContext();
        var broken = await context.File("broken.png");
        var good = await context.File("good.png");
        context.Resources.OnWrite = (data, path) => path == broken + ".meta" ? Task.FromResult(false) : context.Project.Resources.Write(data, path);

        var imported = Assert.Single(await context.Importer.ReimportPaths(new[] { broken, good }));
        Assert.Equal(good, imported.FullPath);
        Assert.True(context.Registry.TryGetByPath(good, out _));
        Assert.False(context.Registry.TryGetByPath(broken, out _));
        Assert.Single(context.Logger.Entries, x => x.Exception != null);
        Assert.False(context.Importer.IsImporting.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }

    /// <summary>Partial import refreshes only the registry indicated by behaviour or shader targets.</summary>
    [Theory]
    [InlineData("code.h", true, 1, 0)]
    [InlineData("effect.RSHADER", false, 0, 1)]
    [InlineData("image.png", false, 0, 0)]
    public async Task PartialImportRoutesRegistryRefresh(string name, bool isBehaviour, int behaviours, int shaders)
    {
        using var context = new TestContext();
        var file = await context.File(name);
        context.BehaviourFiles.OnIsBehaviour = path => path == file && isBehaviour;
        await context.Importer.ReimportPaths(new[] { file });
        Assert.Equal(behaviours, context.Behaviours.RefreshCount);
        Assert.Equal(shaders, context.Shaders.RefreshCount);
    }

    /// <summary>Scene targets refresh loaded entity components and save the resulting scene without refreshing unrelated registries.</summary>
    [Fact]
    public async Task SceneImportRefreshesComponentsAndWritesUpdatedScene()
    {
        using var context = new TestContext();
        var path = await context.File("level.scene", "{}");
        var scene = new Scene("level");
        var entity = new GameEntity(1, "before");
        scene.AddEntity(entity);
        context.Assets.Scenes[path] = scene;
        var refreshed = new List<GameEntity>();
        context.Components.OnRefresh = target => { refreshed.Add(target); target.SetName("refreshed"); };

        await context.Importer.ReimportPaths(new[] { path });
        Assert.Equal(new[] { path }, context.Assets.LoadedPaths);
        Assert.Same(entity, Assert.Single(refreshed));
        Assert.Contains("refreshed", await File.ReadAllTextAsync(path));
        Assert.Equal(0, context.Behaviours.RefreshCount);
        Assert.Equal(0, context.Shaders.RefreshCount);
    }

    /// <summary>No targets bypass import lifecycle, while a gated import rejects reentry and releases flags after completion.</summary>
    [Fact]
    public async Task EmptyTargetsAndConcurrentImportDoNotStartExtraProcedures()
    {
        using var context = new TestContext();
        var events = 0;
        context.Importer.ImportedAssetsEvent += () => events++;
        Assert.Empty(await context.Importer.ReimportPaths(Array.Empty<string>()));
        Assert.Equal(0, events);
        var path = await context.File("image.png");
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Resources.OnWrite = async (data, target) => { await gate.Task; return await context.Project.Resources.Write(data, target); };
        var pending = context.Importer.ReimportPaths(new[] { path });
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.True(context.Importer.IsImporting.Value);
            Assert.Single(context.Procedures.ActiveProcedures);
            Assert.Empty(await context.Importer.ReimportAll());
            Assert.Single(context.Resources.Writes);
            gate.SetResult(true);
            Assert.Single(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(context.Importer.IsImporting.Value);
            Assert.Empty(context.Procedures.ActiveProcedures);
            Assert.Equal(1, events);
        }
        finally
        {
            gate.TrySetResult(true);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>A registry refresh exception completes the import procedure and permits retry on the same service.</summary>
    [Fact]
    public async Task RefreshFailureResetsLifecycleAndAllowsRetry()
    {
        using var context = new TestContext();
        var path = await context.File("image.png");
        context.Behaviours.OnRefresh = () => Task.FromException(new InvalidOperationException("refresh"));
        Assert.Single(await context.Importer.ReimportAll());
        Assert.False(context.Importer.IsImporting.Value);
        Assert.Empty(context.Procedures.ActiveProcedures);
        Assert.Equal(0, context.Shaders.RefreshCount);
        Assert.Contains(context.Logger.Entries, entry => entry.Message.Contains("refresh", StringComparison.Ordinal));
        context.Behaviours.OnRefresh = () => Task.CompletedTask;
        Assert.Equal(path, Assert.Single(await context.Importer.ReimportAll()).FullPath);
        Assert.Equal(1, context.Shaders.RefreshCount);
        Assert.Empty(context.Procedures.ActiveProcedures);
    }
}
