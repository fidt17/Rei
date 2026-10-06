using System.Collections.Concurrent;
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
        public ConcurrentQueue<(string AssetPath, string DestinationPath, long Offset)> Calls { get; } = new();
        public Exception? ExceptionToThrow { get; set; }
        public Action<string>? BeforeBuild { get; set; }
        public Action<string>? AfterBuild { get; set; }
        public bool SkipOutput { get; set; }
        public bool IsEngineRunning => false;

        public long BuildAsset(string assetPath, string destinationFile, long offset)
        {
            Calls.Enqueue((assetPath, destinationFile, offset));
            BeforeBuild?.Invoke(assetPath);
            if (ExceptionToThrow != null) throw ExceptionToThrow;
            if (SkipOutput) return 0;
            var bytes = Outputs[assetPath];
            File.WriteAllBytes(destinationFile, bytes);
            AfterBuild?.Invoke(assetPath);
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

    /// <summary>Cached bytes and newly converted files are packed only after every conversion has finished.</summary>
    [Fact]
    public async Task TestBuildAssetsPreparesAllFilesBeforePacking()
    {
        using var directory = new TemporaryDirectory();
        var first = await CreateAsset(directory, "first", "first.mat", new byte[] { 10 });
        var second = await CreateAsset(directory, "second", "second.mat", new byte[] { 20 });
        var third = await CreateAsset(directory, "third", "third.mat", new byte[] { 30 });
        var engine = new TestEngineApi();
        engine.Outputs[first.FullPath] = new byte[] { 1, 2 };
        engine.Outputs[second.FullPath] = new byte[] { 3, 4, 5 };
        engine.Outputs[third.FullPath] = new byte[] { 6 };
        var pipeline = CreatePipeline("engine");
        var cache = directory.GetPath("cache");
        RunBuild(directory, pipeline, engine, first, cache, "seed.bin");
        engine.Calls.Clear();
        var archive = directory.GetPath("assets.bin");
        var previousBytes = new byte[] { 99, 99 };
        await File.WriteAllBytesAsync(archive, previousBytes);
        engine.BeforeBuild = _ => Assert.Equal(previousBytes, File.ReadAllBytes(archive));

        var result = pipeline.BuildAssets(engine, new[] { first, second, third }, cache, archive);

        Assert.Equal(new[] { second.FullPath, third.FullPath }, engine.Calls.Select(call => call.AssetPath));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, await File.ReadAllBytesAsync(archive));
        Assert.Equal(new[] { "first", "second", "third" }, result.Map.Assets.Select(asset => asset.Id));
        Assert.Equal(new long[] { 0, 2, 5 }, result.Map.Assets.Select(asset => asset.Offset));
        Assert.Equal((3, 1, 2, 6L), (result.Report.TotalAssets, result.Report.CacheHits, result.Report.CacheMisses, result.Report.TotalBytes));
    }

    /// <summary>A later conversion failure prevents packing already prepared files into the destination.</summary>
    [Fact]
    public async Task TestBuildAssetsDoesNotStartPackingWhenConversionFails()
    {
        using var directory = new TemporaryDirectory();
        var first = await CreateAsset(directory, "first", "first.mat", new byte[] { 10 });
        var second = await CreateAsset(directory, "second", "second.mat", new byte[] { 20 });
        var expected = new InvalidOperationException("second conversion failed");
        var engine = new TestEngineApi();
        engine.Outputs[first.FullPath] = new byte[] { 1, 2 };
        engine.BeforeBuild = path => { if (path == second.FullPath) throw expected; };
        var archive = directory.GetPath("assets.bin");
        var cache = directory.GetPath("cache");
        var previousBytes = new byte[] { 99, 99 };
        await File.WriteAllBytesAsync(archive, previousBytes);

        var actual = Assert.Throws<InvalidOperationException>(() => CreatePipeline("engine").BuildAssets(engine, new[] { first, second }, cache, archive));

        Assert.Same(expected, actual);
        Assert.Equal(2, engine.Calls.Count);
        Assert.Equal(previousBytes, await File.ReadAllBytesAsync(archive));
        Assert.False(File.Exists(Path.Combine(cache, "asset-cache.json")));
    }

    /// <summary>Four model workers overlap while other converters, progress, reports and archive order stay coordinated.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestModelsConvertInParallelWithoutChangingPackOrder(bool forceRebuild)
    {
        using var directory = new TemporaryDirectory();
        var assets = new[]
        {
            await CreateAsset(directory, "first", "first.obj", new byte[] { 10 }),
            await CreateAsset(directory, "texture", "texture.png", new byte[] { 20 }),
            await CreateAsset(directory, "second", "second.fbx", new byte[] { 30 }),
            await CreateAsset(directory, "third", "third.obj", new byte[] { 40 }),
            await CreateAsset(directory, "fourth", "fourth.fbx", new byte[] { 50 }),
            await CreateAsset(directory, "fifth", "fifth.obj", new byte[] { 60 }),
            await CreateAsset(directory, "text", "text.OBJ", new byte[] { 70 })
        };
        var engine = new TestEngineApi();
        for (var i = 0; i < assets.Length; i++) engine.Outputs[assets[i].FullPath] = new byte[] { (byte)(i + 1), (byte)(i + 100) };
        var pipeline = CreatePipeline("engine");
        var cache = directory.GetPath("cache");
        RunBuild(directory, pipeline, engine, assets[3], cache, "seed.bin");
        engine.Calls.Clear();
        using var firstStarted = new ManualResetEventSlim();
        using var secondFinished = new ManualResetEventSlim();
        var modelPaths = new HashSet<string> { assets[0].FullPath, assets[2].FullPath, assets[3].FullPath, assets[4].FullPath, assets[5].FullPath };
        var firstBatchPaths = (forceRebuild ? new[] { 0, 2, 3, 4 } : new[] { 0, 2, 4, 5 }).Select(index => assets[index].FullPath).ToHashSet();
        using var firstBatchStarted = new CountdownEvent(4);
        var completionOrder = new ConcurrentQueue<string>();
        var sync = new object();
        var active = 0;
        var peak = 0;
        var coordinatorThread = Environment.CurrentManagedThreadId;
        engine.BeforeBuild = path =>
        {
            if (!modelPaths.Contains(path))
            {
                Assert.Equal(coordinatorThread, Environment.CurrentManagedThreadId);
                lock (sync) Assert.Equal(0, active);
                return;
            }
            lock (sync) { active++; peak = Math.Max(peak, active); Assert.InRange(active, 1, 4); }
            if (firstBatchPaths.Contains(path))
            {
                firstBatchStarted.Signal();
                Assert.True(firstBatchStarted.Wait(TimeSpan.FromSeconds(5)), "Four models must be allowed to run concurrently.");
            }
            if (path == assets[0].FullPath)
            {
                firstStarted.Set();
                Assert.True(secondFinished.Wait(TimeSpan.FromSeconds(5)), "Second model must complete while first is still running.");
            }
            if (path == assets[2].FullPath) Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
        };
        engine.AfterBuild = path =>
        {
            if (!modelPaths.Contains(path)) return;
            lock (sync) active--;
            completionOrder.Enqueue(path);
            if (path == assets[2].FullPath) secondFinished.Set();
        };
        var progress = new List<int>();
        var archive = directory.GetPath("assets.bin");

        var result = pipeline.BuildAssets(engine, assets, cache, archive, forceRebuild, info =>
        {
            Assert.Equal(coordinatorThread, Environment.CurrentManagedThreadId);
            Assert.Equal(assets.Length, info.TotalAssets);
            progress.Add(info.CurrentAssetIndex);
        });

        Assert.Equal(4, peak);
        Assert.Equal(0, active);
        var completedPaths = completionOrder.ToArray();
        Assert.True(Array.IndexOf(completedPaths, assets[2].FullPath) < Array.IndexOf(completedPaths, assets[0].FullPath));
        Assert.Equal(forceRebuild ? 7 : 6, engine.Calls.Count);
        Assert.Equal(forceRebuild ? 0 : 1, result.Report.CacheHits);
        Assert.Equal(Enumerable.Range(1, assets.Length), progress);
        Assert.Equal(assets.SelectMany(asset => engine.Outputs[asset.FullPath]), await File.ReadAllBytesAsync(archive));
        Assert.Equal(assets.Select(asset => asset.Meta.AssetId), result.Map.Assets.Select(asset => asset.Id));
        Assert.Equal(new long[] { 0, 2, 4, 6, 8, 10, 12 }, result.Map.Assets.Select(asset => asset.Offset));
        Assert.Equal(assets.Where((_, index) => forceRebuild || index != 3).Select(asset => asset.Meta.AssetId), result.Report.BuiltAssets.Select(asset => asset.AssetId));
    }

    /// <summary>Failure drains the active model batch before returning and prevents later batches and packing.</summary>
    [Fact]
    public async Task TestModelFailureWaitsForOtherWorkerAndStopsLaterBatches()
    {
        using var directory = new TemporaryDirectory();
        var slow = await CreateAsset(directory, "slow", "slow.obj", new byte[] { 10 });
        var failing = await CreateAsset(directory, "failing", "failing.fbx", new byte[] { 20 });
        var third = await CreateAsset(directory, "third", "third.obj", new byte[] { 30 });
        var fourth = await CreateAsset(directory, "fourth", "fourth.fbx", new byte[] { 40 });
        var later = await CreateAsset(directory, "later", "later.obj", new byte[] { 50 });
        var engine = new TestEngineApi();
        foreach (var asset in new[] { slow, third, fourth }) engine.Outputs[asset.FullPath] = new byte[] { 1, 2 };
        using var firstBatchStarted = new CountdownEvent(4);
        using var slowStarted = new ManualResetEventSlim();
        using var failureStarted = new ManualResetEventSlim();
        using var releaseSlow = new ManualResetEventSlim();
        var expected = new InvalidOperationException("model conversion failed");
        engine.BeforeBuild = path =>
        {
            Assert.NotEqual(later.FullPath, path);
            firstBatchStarted.Signal();
            Assert.True(firstBatchStarted.Wait(TimeSpan.FromSeconds(5)));
            if (path == slow.FullPath)
            {
                slowStarted.Set();
                Assert.True(releaseSlow.Wait(TimeSpan.FromSeconds(5)));
            }
            if (path == failing.FullPath)
            {
                Assert.True(slowStarted.Wait(TimeSpan.FromSeconds(5)));
                failureStarted.Set();
                throw expected;
            }
        };
        var archive = directory.GetPath("assets.bin");
        var cache = directory.GetPath("cache");
        var previousBytes = new byte[] { 99 };
        await File.WriteAllBytesAsync(archive, previousBytes);
        var build = Task.Run(() => CreatePipeline("engine").BuildAssets(engine, new[] { slow, failing, third, fourth, later }, cache, archive));
        try
        {
            Assert.True(failureStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(build.IsCompleted);
        }
        finally
        {
            releaseSlow.Set();
            try { await build; } catch { }
        }
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => build);

        Assert.Same(expected, actual);
        Assert.Equal(4, engine.Calls.Count);
        Assert.DoesNotContain(engine.Calls, call => call.AssetPath == later.FullPath);
        foreach (var asset in new[] { slow, third, fourth })
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(engine.Calls.Single(call => call.AssetPath == asset.FullPath).DestinationPath));
        Assert.Equal(previousBytes, await File.ReadAllBytesAsync(archive));
        Assert.False(File.Exists(Path.Combine(cache, "asset-cache.json")));
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
        await File.WriteAllBytesAsync(packed, new byte[] { 99 });

        var result = CreatePipeline("engine").BuildAssets(new TestEngineApi(), Array.Empty<AssetInfo>(), directory.GetPath("cache"), packed);

        Assert.Empty(await File.ReadAllBytesAsync(packed));
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
