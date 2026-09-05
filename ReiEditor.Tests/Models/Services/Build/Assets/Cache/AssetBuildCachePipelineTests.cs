using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.Assets.Cache;

/// <summary>Verifies cached asset builds, packing order, offsets, reports, and progress.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class AssetBuildCachePipelineTests
{
    /// <summary>Supplies deterministic engine version to real cache service.</summary>
    private sealed class TestEngineSettingsProvider(string version) : ReiEditor.Models.Services.Engine.Settings.IEngineSettingsProvider
    {
        public Task InitializeAsync() => throw new NotSupportedException();
        public string GetEnginePath() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => throw new NotSupportedException();
        public string GetEngineReleaseIncludeDir() => throw new NotSupportedException();
        public string GetEngineSourceIncludes() => throw new NotSupportedException();
        public string GetEngineResourcesDir() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => version;
    }

    /// <summary>Writes configured bytes instead of invoking native asset builder.</summary>
    private sealed class TestEngineApi : IEngineApi
    {
        public Dictionary<string, byte[]> Outputs { get; } = new(StringComparer.Ordinal);
        public List<(string AssetPath, string DestinationPath, long Offset)> Calls { get; } = new();
        public Exception? ExceptionToThrow { get; set; }
        public bool SkipOutput { get; set; }
        public bool IsEngineRunning => false;

        public long BuildAsset(string assetPath, string destinationFile, long offset)
        {
            Calls.Add((assetPath, destinationFile, offset));
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            if (SkipOutput) return 0;
            var bytes = Outputs[assetPath];
            File.WriteAllBytes(destinationFile, bytes);
            return bytes.Length;
        }

        public IntPtr CreateEngine(string resourcesDir, EngineRunMode mode) => throw new NotSupportedException();
        public void Start(IntPtr enginePtr) => throw new NotSupportedException();
        public void Shutdown(IntPtr enginePtr, int exitCode) => throw new NotSupportedException();
        public void DestroyEngine(IntPtr enginePtr) => throw new NotSupportedException();
        public void AddLogCallback(IntPtr ptr) => throw new NotSupportedException();
        public void AddEngineStartCallback(IntPtr callback) => throw new NotSupportedException();
        public void AddShutdownCallback(IntPtr callback) => throw new NotSupportedException();
        public void AddEditorInputCallback(IntPtr callback) => throw new NotSupportedException();
        public Task<IntPtr> CreateEngineWindow() => throw new NotSupportedException();
        public IntPtr GetWindowHandle(IntPtr windowPtr) => throw new NotSupportedException();
        public void ResizeWindow(IntPtr windowPtr, int width, int height) => throw new NotSupportedException();
        public void ChangeRenderMode(RenderMode mode, bool isUiRenderingEnabled) => throw new NotSupportedException();
        public void SetEditorGridSettings(SetViewportGridSettingsRequest settings) => throw new NotSupportedException();
        public void ChangeTransformationMode(TransformationMode mode, bool worldSpace) => throw new NotSupportedException();
        public int GetTransformationMode() => throw new NotSupportedException();
        public bool RequestFrameCapture(IntPtr callback) => throw new NotSupportedException();
        public void MarkEngineStopped() => throw new NotSupportedException();
        public void SetDllPtr(IntPtr ptr) => throw new NotSupportedException();
        public void Invoke(Type delegateType, string methodName = "", params object?[]? args) => throw new NotSupportedException();
        public T Invoke<T>(Type delegateType, string methodName, params object?[]? args) => throw new NotSupportedException();
        public Task<T> InvokeAsync<T>(Type delegateType, string methodName, params object?[]? args) => throw new NotSupportedException();
    }

    /// <summary>Mixed cache hit and miss concatenate exact bytes, compute offsets, report counts, and publish one-based progress.</summary>
    [Fact]
    public async Task TestBuildAssetsPacksMixedHitAndMissWithOffsetsAndProgress()
    {
        using var directory = new TemporaryDirectory();
        var first = await CreateAsset(directory, "b", "first.mat", new byte[] { 10 });
        var second = await CreateAsset(directory, "a", "second.png", new byte[] { 20 });
        var engine = new TestEngineApi();
        engine.Outputs[first.FullPath] = new byte[] { 1, 2, 3 };
        engine.Outputs[second.FullPath] = new byte[] { 4, 5 };
        var pipeline = CreatePipeline("engine");
        var cacheDirectory = directory.GetPath("cache");
        var seedOutput = directory.GetPath("seed.bin");
        await File.WriteAllBytesAsync(seedOutput, Array.Empty<byte>());
        pipeline.BuildAssets(engine, new[] { first }, cacheDirectory, seedOutput);
        var packed = directory.GetPath("assets.bin");
        await File.WriteAllBytesAsync(packed, Array.Empty<byte>());
        var progress = new List<(int Current, int Total, string Path)>();

        var result = pipeline.BuildAssets(engine, new[] { first, second }, cacheDirectory, packed, onAssetBuilding: info =>
            progress.Add((info.CurrentAssetIndex, info.TotalAssets, info.AssetPath)));

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await File.ReadAllBytesAsync(packed));
        var map = result.Map.Assets.ToArray();
        Assert.Equal(("b", "first.mat", first.FullPath, "assets.bin", 0L), (map[0].Id, map[0].Name, map[0].AssetPath, map[0].Path, map[0].Offset));
        Assert.Equal(("a", "second.png", second.FullPath, "assets.bin", 3L), (map[1].Id, map[1].Name, map[1].AssetPath, map[1].Path, map[1].Offset));
        Assert.Equal((2, 1, 1, 5L), (result.Report.TotalAssets, result.Report.CacheHits, result.Report.CacheMisses, result.Report.TotalBytes));
        var built = Assert.Single(result.Report.BuiltAssets);
        Assert.Equal(("a", second.FullPath, 2L), (built.AssetId, built.AssetPath, built.SizeBytes));
        Assert.Equal(new[] { (1, 2, first.FullPath), (2, 2, second.FullPath) }, progress);
        Assert.Equal(2, engine.Calls.Count);
    }

    /// <summary>Persisted hit skips native build while force and changed content each rebuild asset.</summary>
    [Fact]
    public async Task TestBuildAssetsHonorsHitForceAndContentChange()
    {
        using var directory = new TemporaryDirectory();
        var asset = await CreateAsset(directory, "id", "asset.mat", new byte[] { 1 });
        var engine = new TestEngineApi();
        engine.Outputs[asset.FullPath] = new byte[] { 7, 8 };
        var pipeline = CreatePipeline("engine");
        var cache = directory.GetPath("cache");

        var miss = RunBuild(directory, pipeline, engine, asset, cache, "miss.bin");
        var hit = RunBuild(directory, pipeline, engine, asset, cache, "hit.bin");
        var forced = RunBuild(directory, pipeline, engine, asset, cache, "forced.bin", force: true);
        await File.WriteAllBytesAsync(asset.FullPath, new byte[] { 2 });
        var changed = RunBuild(directory, pipeline, engine, asset, cache, "changed.bin");

        Assert.Equal((0, 1), (miss.Report.CacheHits, miss.Report.CacheMisses));
        Assert.Equal((1, 0), (hit.Report.CacheHits, hit.Report.CacheMisses));
        Assert.Equal((0, 1), (forced.Report.CacheHits, forced.Report.CacheMisses));
        Assert.Equal((0, 1), (changed.Report.CacheHits, changed.Report.CacheMisses));
        Assert.Equal(3, engine.Calls.Count);
    }

    /// <summary>Empty input writes manifest and produces empty map and zeroed report.</summary>
    [Fact]
    public async Task TestBuildAssetsHandlesEmptyInput()
    {
        using var directory = new TemporaryDirectory();
        var packed = directory.GetPath("assets.bin");
        await File.WriteAllBytesAsync(packed, Array.Empty<byte>());

        var result = CreatePipeline("engine").BuildAssets(new TestEngineApi(), Array.Empty<AssetInfo>(), directory.GetPath("cache"), packed);

        Assert.Empty(result.Map.Assets);
        Assert.Equal((0, 0, 0, 0L), (result.Report.TotalAssets, result.Report.CacheHits, result.Report.CacheMisses, result.Report.TotalBytes));
        Assert.True(File.Exists(directory.GetPath("cache", "asset-cache.json")));
    }

    /// <summary>Zero-byte output is not cached and is rebuilt on next attempt while retaining zero-offset map entry.</summary>
    [Fact]
    public async Task TestBuildAssetsRetriesZeroByteOutput()
    {
        using var directory = new TemporaryDirectory();
        var asset = await CreateAsset(directory, "id", "empty.mat", new byte[] { 1 });
        var engine = new TestEngineApi();
        engine.Outputs[asset.FullPath] = Array.Empty<byte>();
        var pipeline = CreatePipeline("engine");
        var cache = directory.GetPath("cache");

        var first = RunBuild(directory, pipeline, engine, asset, cache, "first.bin");
        var second = RunBuild(directory, pipeline, engine, asset, cache, "second.bin");

        Assert.Equal(2, engine.Calls.Count);
        Assert.Equal(0, Assert.Single(first.Map.Assets).Offset);
        Assert.Equal(0, Assert.Single(second.Map.Assets).Offset);
        Assert.Empty(first.Report.BuiltAssets);
        Assert.Empty(Directory.GetFiles(cache, "*.cache"));
    }

    /// <summary>Native exception propagates and prevents manifest persistence.</summary>
    [Fact]
    public async Task TestBuildAssetsPropagatesEngineFailureWithoutSavingManifest()
    {
        using var directory = new TemporaryDirectory();
        var asset = await CreateAsset(directory, "id", "asset.mat", new byte[] { 1 });
        var expected = new InvalidOperationException("build failed");
        var engine = new TestEngineApi { ExceptionToThrow = expected };
        var cache = directory.GetPath("cache");
        var output = directory.GetPath("assets.bin");
        await File.WriteAllBytesAsync(output, Array.Empty<byte>());

        var actual = Assert.Throws<InvalidOperationException>(() => CreatePipeline("engine").BuildAssets(engine, new[] { asset }, cache, output));

        Assert.Same(expected, actual);
        Assert.False(File.Exists(Path.Combine(cache, "asset-cache.json")));
    }

    /// <summary>Missing native output fails before invalid cache metadata can be saved.</summary>
    [Fact]
    public async Task TestBuildAssetsRejectsMissingEngineOutput()
    {
        using var directory = new TemporaryDirectory();
        var asset = await CreateAsset(directory, "id", "asset.mat", new byte[] { 1 });
        var engine = new TestEngineApi { SkipOutput = true };
        var cache = directory.GetPath("cache");
        var output = directory.GetPath("assets.bin");
        await File.WriteAllBytesAsync(output, Array.Empty<byte>());

        Assert.Throws<FileNotFoundException>(() => CreatePipeline("engine").BuildAssets(engine, new[] { asset }, cache, output));
        Assert.False(File.Exists(Path.Combine(cache, "asset-cache.json")));
    }

    /// <summary>Creates real cache pipeline with deterministic engine version.</summary>
    private static AssetBuildCachePipeline CreatePipeline(string engineVersion)
    {
        var cacheService = new AssetBuildCacheService(
            new TestLogger<AssetBuildCacheService>(),
            new TestEngineSettingsProvider(engineVersion));
        return new AssetBuildCachePipeline(new TestLogger<AssetBuildCachePipeline>(), cacheService);
    }

    /// <summary>Runs one build into a newly truncated output file and returns report.</summary>
    private static AssetsBuildResult RunBuild(TemporaryDirectory directory, AssetBuildCachePipeline pipeline, TestEngineApi engine, AssetInfo asset, string cache, string outputName, bool force = false)
    {
        var output = directory.GetPath(outputName);
        File.WriteAllBytes(output, Array.Empty<byte>());
        return pipeline.BuildAssets(engine, new[] { asset }, cache, output, force);
    }

    /// <summary>Creates source asset containing known bytes.</summary>
    private static async Task<AssetInfo> CreateAsset(TemporaryDirectory directory, string id, string fileName, byte[] sourceBytes)
    {
        var path = directory.GetPath(fileName);
        await File.WriteAllBytesAsync(path, sourceBytes);
        return new AssetInfo(new AssetMeta(id), path);
    }

}
