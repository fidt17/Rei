using ReiEditor.Models.Resources;
using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation.Behaviour;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.Assets.Creation.Behaviour;

/// <summary>
/// Verifies behaviour source generation, selected lifecycle overrides, metadata, and failure handling.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class BehaviourCreationUtilityTests
{
    /// <summary>Allocates fixed asset identity.</summary>
    private sealed class TestAssetCreator : IAssetCreator
    {
        /// <summary>Returns fixed asset identity.</summary>
        public string AllocateAssetId() => "asset-id";

        /// <summary>Rejects unused create overload.</summary>
        public Task<bool> Create(Asset asset, string projectPath) => throw new NotSupportedException();

        /// <summary>Rejects unused identified create overload.</summary>
        public Task<bool> Create(Asset asset, string id, string projectPath) => throw new NotSupportedException();
    }

    /// <summary>Provides empty behaviour registry and fixed allocated ID.</summary>
    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = new Dictionary<int, BehaviourAssetInfo>();

        /// <summary>Rejects unused behaviour lookup.</summary>
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => throw new NotSupportedException();

        /// <summary>Rejects unused name lookup.</summary>
        public int? GetIdByName(string name) => throw new NotSupportedException();

        /// <summary>Returns fixed new behaviour identity.</summary>
        public int AllocateBehaviourId() => 73;

        /// <summary>Rejects unused refresh.</summary>
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>Captures created metadata.</summary>
    private sealed class TestMetaFilesService : IMetaFilesService
    {
        public AssetMeta? CreatedMeta { get; private set; }
        public string? CreatedPath { get; private set; }

        /// <summary>Captures metadata creation.</summary>
        public Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath)
        {
            CreatedMeta = meta;
            CreatedPath = assetPath;
            return Task.FromResult(new ObjectFile<AssetMeta>(meta, assetPath + ".meta"));
        }

        /// <summary>Rejects unused target regeneration.</summary>
        public Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused directory regeneration.</summary>
        public Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused asset regeneration.</summary>
        public Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata move.</summary>
        public void MoveMetaFile(string oldAssetPath, string newAssetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused metadata deletion.</summary>
        public void DeleteMetaFile(string assetPath) => throw new NotSupportedException();

        /// <summary>Rejects unused invalid metadata cleanup.</summary>
        public Task DeleteInvalidMetaFiles() => throw new NotSupportedException();
    }

    /// <summary>Captures partial import paths.</summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting { get; } = new Observable<bool>(false);
        public List<IReadOnlyList<string>> Requests { get; } = new();

        /// <summary>Rejects unused full import.</summary>
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();

        /// <summary>Captures partial import paths.</summary>
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) { Requests.Add(paths.ToArray()); return Task.FromResult(new List<AssetInfo>()); }
    }


    /// <summary>
    /// Creation writes header and source with only selected overrides, assigns identities, and imports both files.
    /// </summary>
    [Fact]
    public async Task CreateBehaviourWritesSelectedOverridesAndMetadata()
    {
        using var project = new TemporaryProjectFixture();
        var target = project.Directory.GetPath("Scripts");
        Directory.CreateDirectory(target);
        var resources = new TestResourceService(project.Resources);
        var metaFiles = new TestMetaFilesService();
        var importer = new TestAssetImporter();
        var utility = new BehaviourCreationUtility(resources, new TestAssetCreator(), new TestBehaviourRegistry(), metaFiles, importer, new TestLogger<BehaviourCreationUtility>());

        var result = await utility.CreateBehaviourAsync(new BehaviourCreationSettings(target, "Player", true, false, true, false));

        Assert.True(result);
        var header = await File.ReadAllTextAsync(Path.Combine(target, "Player.h"));
        var source = await File.ReadAllTextAsync(Path.Combine(target, "Player.cpp"));
        Assert.Contains("class Player : public rei::Behaviour", header);
        Assert.Contains("BEHAVIOUR_BODY(Player)", header);
        Assert.Contains("void Init() override;", header);
        Assert.Contains("void Update() override;", header);
        Assert.DoesNotContain("void Start() override;", header);
        Assert.DoesNotContain("void Dispose() override;", header);
        Assert.Contains("void Player::Init()", source);
        Assert.Contains("void Player::Update()", source);
        Assert.DoesNotContain("Player::Start", source);
        Assert.Equal("asset-id", metaFiles.CreatedMeta!.AssetId);
        Assert.Equal(73, metaFiles.CreatedMeta.GetData<BehaviourMeta>(BehaviourMeta.Key)!.BehaviourId);
        Assert.Equal(Path.Combine(target, "Player.h"), metaFiles.CreatedPath);
        Assert.Equal([Path.Combine(target, "Player.h"), Path.Combine(target, "Player.cpp")], Assert.Single(importer.Requests));
    }

    /// <summary>
    /// Invalid names reject creation before any write or import.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("2Player")]
    [InlineData("Player Name")]
    public async Task InvalidNameCreatesNoFiles(string name)
    {
        using var project = new TemporaryProjectFixture();
        var resources = new TestResourceService(project.Resources);
        var importer = new TestAssetImporter();
        var utility = new BehaviourCreationUtility(resources, new TestAssetCreator(), new TestBehaviourRegistry(), new TestMetaFilesService(), importer, new TestLogger<BehaviourCreationUtility>());

        var result = await utility.CreateBehaviourAsync(new BehaviourCreationSettings(project.Directory.RootPath, name, false, false, false, false));

        Assert.False(result);
        Assert.Empty(resources.Writes);
        Assert.Empty(importer.Requests);
    }

    /// <summary>
    /// Failed source write reports failure and does not create metadata or request import.
    /// </summary>
    [Fact]
    public async Task SourceWriteFailureStopsMetadataAndImport()
    {
        using var project = new TemporaryProjectFixture();
        var resources = new TestResourceService(project.Resources);
        resources.OnWrite = async (data, path) =>
        {
            if (path.EndsWith(".cpp", StringComparison.Ordinal)) return false;
            return await project.Resources.Write(data, path);
        };
        var metaFiles = new TestMetaFilesService();
        var importer = new TestAssetImporter();
        var utility = new BehaviourCreationUtility(resources, new TestAssetCreator(), new TestBehaviourRegistry(), metaFiles, importer, new TestLogger<BehaviourCreationUtility>());

        var result = await utility.CreateBehaviourAsync(new BehaviourCreationSettings(project.Directory.RootPath, "Player", false, false, false, false));

        Assert.False(result);
        Assert.True(File.Exists(project.Directory.GetPath("Player.h")));
        Assert.False(File.Exists(project.Directory.GetPath("Player.cpp")));
        Assert.Null(metaFiles.CreatedMeta);
        Assert.Empty(importer.Requests);
    }

}
