using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.ProjectManagement.Creation;
using ReiEditor.Models.ProjectManagement.Template;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

/// <summary>Verifies behaviour registry refresh IDs, publication, metadata creation, and failure isolation.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class BehaviourRegistryTests : IDisposable
{
    /// <summary>Supplies behaviour files and matching metadata to registry refresh.</summary>
    private sealed class TestBehaviourFileUtility : IBehaviourFileUtility
    {
        public List<BehaviourFileUtility.BehaviourPathData> Files { get; } = new();
        public List<ObjectFile<AssetMeta>> Metas { get; } = new();

        /// <summary>Returns configured behaviour files.</summary>
        public List<BehaviourFileUtility.BehaviourPathData> GetAllBehaviours() => Files;

        /// <summary>Returns configured metadata files.</summary>
        public Task<List<ObjectFile<AssetMeta>>> GetAllBehaviourMetas() => Task.FromResult(Metas);

        /// <summary>Extracts behaviour macro argument.</summary>
        public bool TryGetBehaviourNameFrom(string text, out string name)
        {
            const string PREFIX = "BEHAVIOUR_BODY(";
            var start = text.IndexOf(PREFIX, StringComparison.Ordinal);
            if (start < 0)
            {
                name = "";
                return false;
            }

            start += PREFIX.Length;
            var end = text.IndexOf(')', start);
            name = text[start..end];
            return true;
        }

        /// <summary>File classification is outside registry refresh scenario.</summary>
        public Task<bool> IsBehaviourFile(string path) => throw new NotSupportedException();
    }

    /// <summary>Allocates stable asset IDs for newly created metadata.</summary>
    private sealed class TestAssetCreator : IAssetCreator
    {
        private int _id;

        /// <summary>Allocates next test asset ID.</summary>
        public string AllocateAssetId() => $"asset-{++_id}";

        /// <summary>Asset creation is outside registry refresh scenario.</summary>
        public Task<bool> Create(Asset asset, string projectPath) => throw new NotSupportedException();

        /// <summary>Asset creation is outside registry refresh scenario.</summary>
        public Task<bool> Create(Asset asset, string id, string projectPath) => throw new NotSupportedException();
    }

    /// <summary>Creates metadata under configured isolated paths and records it.</summary>
    private sealed class TestMetaFilesService : IMetaFilesService
    {
        public List<ObjectFile<AssetMeta>> Created { get; } = new();

        /// <summary>Creates in-memory metadata result at requested asset path.</summary>
        public Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string assetPath)
        {
            var result = new ObjectFile<AssetMeta>(meta, assetPath + ".meta");
            Created.Add(result);
            return Task.FromResult(result);
        }

        /// <summary>Metadata regeneration is outside registry refresh scenario.</summary>
        public Task RegenerateMetaFilesForTargets(IEnumerable<string> targets, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Metadata regeneration is outside registry refresh scenario.</summary>
        public Task RegenerateMetaFilesInDirectory(string directoryPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Metadata regeneration is outside registry refresh scenario.</summary>
        public Task RegenerateMetaFileForAsset(string assetPath, IMetaFileRegenerationPolicy policy) => throw new NotSupportedException();

        /// <summary>Metadata move is outside registry refresh scenario.</summary>
        public void MoveMetaFile(string oldAssetPath, string newAssetPath) => throw new NotSupportedException();

        /// <summary>Metadata deletion is outside registry refresh scenario.</summary>
        public void DeleteMetaFile(string assetPath) => throw new NotSupportedException();

        /// <summary>Metadata cleanup is outside registry refresh scenario.</summary>
        public Task DeleteInvalidMetaFiles() => throw new NotSupportedException();
    }

    /// <summary>Records source includes passed to project solution update.</summary>
    private sealed class TestSolutionGenerator : ISolutionGenerator
    {
        public List<string> Includes { get; } = new();

        /// <summary>Solution creation is outside registry refresh scenario.</summary>
        public Task<SolutionGenerationResult> GenerateSolution(ProjectCreationConfiguration projectCreationConfiguration) => throw new NotSupportedException();

        /// <summary>Project file rewrite is outside registry refresh scenario.</summary>
        public Task UpdateProjectFile(string projectFilePath) => throw new NotSupportedException();

        /// <summary>Records normalized source includes.</summary>
        public Task AddSourceFiles(string projectFilePath, IEnumerable<string> includes)
        {
            Includes.AddRange(includes);
            return Task.CompletedTask;
        }
    }

    /// <summary>Provides isolated engine source directory.</summary>
    private sealed class TestEngineSettingsProvider(string enginePath) : IEngineSettingsProvider
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => enginePath;
        public string GetEngineDebugIncludeDir() => enginePath;
        public string GetEngineReleaseIncludeDir() => enginePath;
        public string GetEngineSourceIncludes() => enginePath;
        public string GetEngineResourcesDir() => enginePath;
        public string GetEngineBehavioursDir() => enginePath;
        public string GetEngineVersion() => "test";
    }

    /// <summary>Publishes configured serialization definitions without parsing files.</summary>
    private sealed class TestSerializableObjectsRegistry : ISerializableObjectsRegistry
    {
        public IEnumerable<SerializableObjectInfo> GetObjects() => Array.Empty<SerializableObjectInfo>();
        public Task Refresh() => Task.CompletedTask;
        public SerializableObjectInfo? GetObject(string objectName) => null;
        public SerializableEnum? GetEnum(string enumName) => null;
    }

    private readonly TemporaryProjectFixture _project = new();

    /// <summary>Refresh preserves existing IDs, allocates above max, writes generated source, and updates solution includes.</summary>
    [Fact]
    public async Task RefreshesExistingAndNewBehaviours()
    {
        var fixture = CreateRegistry();
        var existingPath = WriteScript("Existing.h", "namespace game { BEHAVIOUR_BODY(Existing) SERIALIZE int Count; }");
        var newPath = WriteScript("New.h", "namespace game { BEHAVIOUR_BODY(New) REQUIRE_COMPONENT(Existing) }");
        fixture.Files.Files.Add(PathData(existingPath));
        fixture.Files.Files.Add(PathData(newPath));
        fixture.Files.Metas.Add(Meta(existingPath, 5));

        await fixture.Registry.RefreshBehaviours();

        Assert.Equal(5, fixture.Registry.GetIdByName("Existing"));
        Assert.Equal(6, fixture.Registry.GetIdByName("New"));
        Assert.Equal(7, fixture.Registry.AllocateBehaviourId());
        Assert.Single(fixture.MetaFiles.Created);
        Assert.Contains(fixture.Solution.Includes, include => include.EndsWith("Existing.h"));
        Assert.True(File.Exists(_project.Resources.GetProjectPath("Scripts", "Internal", "BehaviourRegistry.cpp")));
    }

    /// <summary>Duplicate-ID refresh fails before replacing last valid published registry snapshot.</summary>
    [Fact]
    public async Task FailedRefreshPreservesPublishedRegistry()
    {
        var fixture = CreateRegistry();
        var firstPath = WriteScript("First.h", "BEHAVIOUR_BODY(First)");
        fixture.Files.Files.Add(PathData(firstPath));
        fixture.Files.Metas.Add(Meta(firstPath, 3));
        await fixture.Registry.RefreshBehaviours();

        var secondPath = WriteScript("Second.h", "BEHAVIOUR_BODY(Second)");
        fixture.Files.Files.Add(PathData(secondPath));
        fixture.Files.Metas.Add(Meta(secondPath, 3));

        await Assert.ThrowsAsync<Exception>(() => fixture.Registry.RefreshBehaviours());

        Assert.Equal(3, fixture.Registry.GetIdByName("First"));
        Assert.Null(fixture.Registry.GetIdByName("Second"));
        Assert.Single(fixture.Registry.Behaviours);
    }

    /// <summary>Creates registry and focused collaborators over isolated project root.</summary>
    private (BehaviourRegistry Registry, TestBehaviourFileUtility Files, TestMetaFilesService MetaFiles, TestSolutionGenerator Solution) CreateRegistry()
    {
        var enginePath = _project.Directory.GetPath("Engine");
        Directory.CreateDirectory(enginePath);
        Directory.CreateDirectory(_project.Resources.GetScriptsPath());
        var sourceFiles = new SourceFilesUtility(_project.Resources, new TestEngineSettingsProvider(enginePath), new TestLogger<SourceFilesUtility>());
        var serializableObjects = new TestSerializableObjectsRegistry();
        var files = new TestBehaviourFileUtility();
        var metaFiles = new TestMetaFilesService();
        var solution = new TestSolutionGenerator();
        var activeProject = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        var project = new Project();
        project.SetProjectFilePath(_project.Directory.GetPath("Registry.reiproj"));
        project.SetProjectVisualStudioProjectPath(_project.Directory.GetPath("Registry.vcxproj"));
        activeProject.OpenProject(project);
        var registry = new BehaviourRegistry(
            new TestAssetCreator(),
            _project.Resources,
            new TestLogger<BehaviourRegistry>(),
            serializableObjects,
            solution,
            activeProject,
            sourceFiles,
            files,
            metaFiles);
        return (registry, files, metaFiles, solution);
    }

    /// <summary>Writes script into isolated project source root.</summary>
    private string WriteScript(string name, string source)
    {
        var path = _project.Resources.GetScriptsPath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source);
        return path;
    }

    /// <summary>Creates behaviour file descriptor.</summary>
    private static BehaviourFileUtility.BehaviourPathData PathData(string path) => new(File.ReadAllText(path), path, path + ".meta", false);

    /// <summary>Creates behaviour metadata file descriptor.</summary>
    private static ObjectFile<AssetMeta> Meta(string path, int behaviourId)
    {
        var meta = new AssetMeta($"asset-{behaviourId}");
        meta.AddData(BehaviourMeta.Key, new BehaviourMeta(behaviourId));
        return new ObjectFile<AssetMeta>(meta, path + ".meta");
    }

    /// <summary>Deletes isolated registry files.</summary>
    public void Dispose() => _project.Dispose();
}
