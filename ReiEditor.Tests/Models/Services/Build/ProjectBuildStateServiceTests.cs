using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies persisted live build snapshots and stage invalidation rules.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class ProjectBuildStateServiceTests : IDisposable
{
    /// <summary>Supplies isolated engine artifact roots and mutable version.</summary>
    private sealed class TestEngineSettingsProvider(string debugDirectory, string releaseDirectory) : IEngineSettingsProvider
    {
        public string Version { get; set; } = "engine-1";
        public Task InitializeAsync() => Task.CompletedTask;
        public string GetEnginePath() => Path.GetDirectoryName(debugDirectory)!;
        public string GetEngineDebugIncludeDir() => debugDirectory;
        public string GetEngineReleaseIncludeDir() => releaseDirectory;
        public string GetEngineSourceIncludes() => "";
        public string GetEngineResourcesDir() => "";
        public string GetEngineBehavioursDir() => "";
        public string GetEngineVersion() => Version;
    }

    private readonly TemporaryProjectFixture _project = new();
    private readonly AssetRegistry _assets = new(new TestLogger<AssetRegistry>());
    private readonly TestLogger<ProjectBuildStateService> _logger = new();
    private readonly EditorBuildOutputService _output;
    private readonly TestEngineSettingsProvider _engine;
    private readonly ProjectBuildStateService _service;
    private readonly BuildExecutionContext _liveContext;

    /// <summary>Creates isolated live output, engine roots, and service dependencies.</summary>
    public ProjectBuildStateServiceTests()
    {
        var activeProject = new ActiveProjectService(new TestLogger<ActiveProjectService>());
        activeProject.OpenProject(_project.Project);
        _output = new EditorBuildOutputService(activeProject);
        _engine = new TestEngineSettingsProvider(
            _project.Directory.GetPath("Engine", "Debug"),
            _project.Directory.GetPath("Engine", "Release"));
        _service = new ProjectBuildStateService(_project.Resources, _assets, _engine, _output, _logger);
        var live = _output.GetLiveOutput();
        _liveContext = new BuildExecutionContext(live.BinDirectoryPath, ClientDllPath: live.ClientDllPath);
    }

    /// <summary>Missing state requests exactly the caller-selected build stages.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TestMissingStateRequestsSelectedStages(bool solution, bool assets)
    {
        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, solution, assets);

        Assert.Equal(solution, state.ShouldBuildSolution);
        Assert.Equal(assets, state.ShouldBuildAssets);
        Assert.Contains("No persisted build state", state.Reason);
    }

    /// <summary>Successful complete snapshot returns no-op while inputs and outputs remain unchanged.</summary>
    [Fact]
    public async Task TestSuccessfulSnapshotReturnsNoOp()
    {
        await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);

        Assert.False(state.ShouldBuildSolution);
        Assert.False(state.ShouldBuildAssets);
        Assert.Contains("up to date", state.Reason);
    }

    /// <summary>Persisted source hash equals independently computed SHA-256 value.</summary>
    [Fact]
    public async Task TestSuccessfulSnapshotStoresIndependentContentHash()
    {
        var sourcePath = await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);

        var document = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()));
        var source = Assert.Single(
            document["SourceFiles"]!.Children<JObject>(),
            x => x.Value<string>("RelativePath")!.EndsWith("State.cpp", StringComparison.Ordinal));
        var expectedHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath))).ToLowerInvariant();
        Assert.Equal(expectedHash, source.Value<string>("ContentHash"));
    }

    /// <summary>Same-size source content change invalidates solution snapshot.</summary>
    [Fact]
    public async Task TestSameSizeSourceChangeRequestsSolutionOnly()
    {
        var sourcePath = await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        await File.WriteAllTextAsync(sourcePath, "BBBB");

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);

        Assert.True(state.ShouldBuildSolution);
        Assert.False(state.ShouldBuildAssets);
        Assert.Contains("source", state.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Same-size registered asset content change invalidates assets without requesting solution.</summary>
    [Fact]
    public async Task TestSameSizeAssetChangeRequestsAssetsOnly()
    {
        await PrepareCompleteBuildFiles();
        var assetPath = _project.Resources.GetProjectPath("Textures", "tracked.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "AAAA");
        _assets.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta("tracked"), assetPath) });
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, false, true);

        await File.WriteAllTextAsync(assetPath, "BBBB");
        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, false, true);

        Assert.False(state.ShouldBuildSolution);
        Assert.True(state.ShouldBuildAssets);
        Assert.Contains("asset", state.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Saving assets-only success retains prior solution snapshot and leaves source change dirty.</summary>
    [Fact]
    public async Task TestAssetsOnlySuccessPreservesSolutionSnapshot()
    {
        var sourcePath = await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        var sourceSnapshot = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()))["SourceFiles"]!.DeepClone();
        await File.WriteAllTextAsync(sourcePath, "BBBB");

        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, false, true);
        var preservedSnapshot = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()))["SourceFiles"];
        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);

        Assert.True(JToken.DeepEquals(sourceSnapshot, preservedSnapshot));
        Assert.True(state.ShouldBuildSolution);
        Assert.False(state.ShouldBuildAssets);
    }

    /// <summary>Saving solution-only success retains prior asset snapshot and leaves asset change dirty.</summary>
    [Fact]
    public async Task TestSolutionOnlySuccessPreservesAssetSnapshot()
    {
        await PrepareCompleteBuildFiles();
        var assetPath = _project.Resources.GetProjectPath("Textures", "partial.png");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "AAAA");
        _assets.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta("partial"), assetPath) });
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        var assetSnapshot = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()))["AssetFiles"]!.DeepClone();
        await File.WriteAllTextAsync(assetPath, "BBBB");

        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);
        var preservedSnapshot = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()))["AssetFiles"];
        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, false, true);

        Assert.True(JToken.DeepEquals(assetSnapshot, preservedSnapshot));
        Assert.False(state.ShouldBuildSolution);
        Assert.True(state.ShouldBuildAssets);
    }

    /// <summary>Engine version change invalidates every requested stage.</summary>
    [Fact]
    public async Task TestEngineVersionChangeInvalidatesRequestedStages()
    {
        await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        _engine.Version = "engine-2";

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);

        Assert.True(state.ShouldBuildSolution);
        Assert.True(state.ShouldBuildAssets);
        Assert.Contains("Engine version changed", state.Reason);
    }

    /// <summary>Started and failed markers prevent persisted snapshot reuse.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestNonReadyStatusInvalidatesSnapshot(bool failed)
    {
        await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        if (failed) _service.MarkBuildFailed(BuildConfigurationEnum.EditorDebug, _liveContext);
        else _service.MarkBuildStarted(BuildConfigurationEnum.EditorDebug, _liveContext);

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);

        Assert.True(state.ShouldBuildSolution);
        Assert.True(state.ShouldBuildAssets);
        Assert.Contains("Persisted build state is", state.Reason);
    }

    /// <summary>Corrupt state logs parse failure and is treated as missing.</summary>
    [Fact]
    public async Task TestCorruptStateRequestsBuildAndLogsFailure()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GetStatePath())!);
        await File.WriteAllTextAsync(GetStatePath(), "{broken");

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);

        Assert.True(state.ShouldBuildSolution);
        Assert.False(state.ShouldBuildAssets);
        Assert.Contains("No persisted build state", state.Reason);
        Assert.Contains(_logger.Entries, entry => entry.Exception != null);
    }

    /// <summary>Outdated format requests rebuild even when snapshot outputs remain present.</summary>
    [Fact]
    public async Task TestOutdatedFormatInvalidatesSnapshot()
    {
        await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        var document = JObject.Parse(await File.ReadAllTextAsync(GetStatePath()));
        document["FormatVersion"] = "1";
        await File.WriteAllTextAsync(GetStatePath(), document.ToString());

        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);

        Assert.True(state.ShouldBuildSolution);
        Assert.True(state.ShouldBuildAssets);
        Assert.Contains("outdated", state.Reason);
    }

    /// <summary>Missing client or asset output invalidates only applicable requested stage.</summary>
    [Fact]
    public async Task TestMissingOutputsInvalidateApplicableStages()
    {
        await PrepareCompleteBuildFiles();
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, _liveContext, true, true);
        File.Delete(_output.GetLiveOutput().ClientDllPath);

        var missingClient = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, true, false);
        Assert.True(missingClient.ShouldBuildSolution);
        Assert.False(missingClient.ShouldBuildAssets);

        await File.WriteAllTextAsync(_output.GetLiveOutput().ClientDllPath, "client");
        File.Delete(Path.Combine(_liveContext.ResourcesDirectoryPath, "assets.bin"));
        var missingAssets = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, _liveContext, false, true);
        Assert.False(missingAssets.ShouldBuildSolution);
        Assert.True(missingAssets.ShouldBuildAssets);
    }

    /// <summary>Non-live contexts neither write state nor claim persisted no-op.</summary>
    [Fact]
    public async Task TestStagingContextDoesNotPersistState()
    {
        var staging = new BuildExecutionContext(_project.Directory.GetPath(".rei_tmp", "build"));

        _service.MarkBuildStarted(BuildConfigurationEnum.EditorDebug, staging);
        await _service.SaveSuccessfulBuild(BuildConfigurationEnum.EditorDebug, staging, true, true);
        var state = await _service.CalculateState(BuildConfigurationEnum.EditorDebug, staging, true, false);

        Assert.False(Directory.Exists(Path.Combine(staging.BuildFolder, ".rei_build_state")));
        Assert.True(state.ShouldBuildSolution);
        Assert.False(state.ShouldBuildAssets);
        Assert.Contains("disabled", state.Reason);
    }

    /// <summary>Creates all source, engine, client, and asset files required by complete snapshot.</summary>
    private async Task<string> PrepareCompleteBuildFiles()
    {
        var sourcePath = _project.Resources.GetScriptsPath("State.cpp");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "AAAA");

        foreach (var directory in new[] { _engine.GetEngineDebugIncludeDir(), _engine.GetEngineReleaseIncludeDir() })
        {
            Directory.CreateDirectory(directory);
            foreach (var fileName in new[] { "Rei.dll", "Rei.lib", "assimp-vc143-mt.dll" })
            {
                await File.WriteAllTextAsync(Path.Combine(directory, fileName), directory + fileName);
            }
        }

        var live = _output.GetLiveOutput();
        Directory.CreateDirectory(live.ClientOutputDirectoryPath);
        await File.WriteAllTextAsync(live.ClientDllPath, "client");
        await File.WriteAllTextAsync(Path.Combine(live.ClientOutputDirectoryPath, "Rei.dll"), "engine");
        await File.WriteAllTextAsync(Path.Combine(live.ClientOutputDirectoryPath, "assimp-vc143-mt.dll"), "assimp");
        Directory.CreateDirectory(live.ResourcesDirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(live.ResourcesDirectoryPath, "assets.bin"), "assets");
        await File.WriteAllTextAsync(Path.Combine(live.ResourcesDirectoryPath, "map.bin"), "map");
        return sourcePath;
    }

    /// <summary>Returns EditorDebug state path under isolated live bin.</summary>
    private string GetStatePath() => Path.Combine(_liveContext.BuildFolder, ".rei_build_state", "EditorDebug.json");

    /// <summary>Deletes isolated state, source, engine, and output files.</summary>
    public void Dispose() => _project.Dispose();
}
