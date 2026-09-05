using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Models.Services.Serialization.Assets;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.Assets;

/// <summary>Verifies complete asset builder packing, map output, session selection, and disposal.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class AssetBuilderTests
{
    /// <summary>Writes configured bytes instead of invoking native asset builder.</summary>
    private sealed class TestEngineApi : IEngineApi
    {
        public Dictionary<string, byte[]> Outputs { get; } = new(StringComparer.Ordinal);
        public List<string> BuiltPaths { get; } = new();
        public Exception? ExceptionToThrow { get; set; }
        public bool IsEngineRunning => false;

        public long BuildAsset(string assetPath, string destinationFile, long offset)
        {
            BuiltPaths.Add(assetPath);
            if (ExceptionToThrow != null) throw ExceptionToThrow;
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

    /// <summary>Records shared and isolated session creation and disposal.</summary>
    private sealed class TestEngineSessionFactory(TestEngineApi engineApi) : IAssetBuildEngineSessionFactory
    {
        public int SharedCalls { get; private set; }
        public List<string> IsolatedPaths { get; } = new();
        public int DisposeCalls { get; private set; }

        public AssetBuildEngineSession CreateSharedSession()
        {
            SharedCalls++;
            return new AssetBuildEngineSession(engineApi, () => DisposeCalls++);
        }

        public AssetBuildEngineSession CreateIsolatedSession(string clientDllPath)
        {
            IsolatedPaths.Add(clientDllPath);
            return new AssetBuildEngineSession(engineApi, () => DisposeCalls++);
        }
    }

    /// <summary>Serializes build maps with production binary layout while retaining map for assertions.</summary>
    private sealed class TestBinarySerializer : IBinarySerializer
    {
        public BuildAssetMap? SerializedMap { get; private set; }

        public void Serialize<T>(T target, BinaryWriter writer)
        {
            SerializedMap = Assert.IsType<BuildAssetMap>(target);
            new BuildAssetMapSerializer().Serialize(SerializedMap, writer);
        }
    }

    /// <summary>Supplies deterministic engine cache version.</summary>
    private sealed class TestEngineSettingsProvider : IEngineSettingsProvider
    {
        public Task InitializeAsync() => throw new NotSupportedException();
        public string GetEnginePath() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => throw new NotSupportedException();
        public string GetEngineReleaseIncludeDir() => throw new NotSupportedException();
        public string GetEngineSourceIncludes() => throw new NotSupportedException();
        public string GetEngineResourcesDir() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => "engine";
    }

    /// <summary>Builder filters and ID-sorts assets, truncates prior pack, writes both maps, reports progress, and disposes shared session.</summary>
    [Fact]
    public async Task TestBuildAssetsCreatesSortedPackAndMapsThroughSharedSession()
    {
        using var directory = new TemporaryDirectory();
        var first = await CreateAsset(directory, "b", "first.mat", new byte[] { 1 });
        var second = await CreateAsset(directory, "a", "second.png", new byte[] { 2 });
        var ignored = await CreateAsset(directory, "c", "ignored.cpp", new byte[] { 3 });
        var engine = new TestEngineApi();
        engine.Outputs[first.FullPath] = new byte[] { 10, 11 };
        engine.Outputs[second.FullPath] = new byte[] { 20, 21, 22 };
        var factory = new TestEngineSessionFactory(engine);
        var serializer = new TestBinarySerializer();
        var builder = CreateBuilder(new[] { first, second, ignored }, factory, serializer);
        var context = new BuildExecutionContext(directory.GetPath("build"));
        Directory.CreateDirectory(context.ResourcesDirectoryPath);
        await File.WriteAllBytesAsync(Path.Combine(context.ResourcesDirectoryPath, "assets.bin"), new byte[] { 99, 99, 99, 99 });
        var progress = new List<AssetBuildProgressInfo>();

        await builder.BuildAssets(context, onAssetBuilding: progress.Add);

        Assert.Equal(new[] { second.FullPath, first.FullPath }, engine.BuiltPaths);
        Assert.Equal(new byte[] { 20, 21, 22, 10, 11 }, await File.ReadAllBytesAsync(Path.Combine(context.ResourcesDirectoryPath, "assets.bin")));
        var map = serializer.SerializedMap!.Assets.ToArray();
        Assert.Equal(new[] { "a", "b" }, map.Select(asset => asset.Id));
        Assert.Equal(new long[] { 0, 3 }, map.Select(asset => asset.Offset));
        Assert.Equal(new[] { 1, 2 }, progress.Select(info => info.CurrentAssetIndex));
        Assert.All(progress, info => Assert.Equal(2, info.TotalAssets));
        Assert.True(new FileInfo(Path.Combine(context.ResourcesDirectoryPath, "map.bin")).Length > 0);
        var jsonAssets = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(context.ResourcesDirectoryPath, "map.json")))["Assets"];
        Assert.Equal(new[] { "a", "b" }, jsonAssets!.Select(token => token["Id"]!.Value<string>()));
        Assert.Equal(1, factory.SharedCalls);
        Assert.Empty(factory.IsolatedPaths);
        Assert.Equal(1, factory.DisposeCalls);
    }

    /// <summary>Explicit client DLL selects isolated session and still disposes it after successful empty build.</summary>
    [Fact]
    public async Task TestBuildAssetsUsesIsolatedSessionForExplicitClientDll()
    {
        using var directory = new TemporaryDirectory();
        var engine = new TestEngineApi();
        var factory = new TestEngineSessionFactory(engine);
        var builder = CreateBuilder(Array.Empty<AssetInfo>(), factory, new TestBinarySerializer());
        var clientDll = directory.GetPath("isolated.dll");

        await builder.BuildAssets(new BuildExecutionContext(directory.GetPath("build"), ClientDllPath: clientDll));

        Assert.Equal(clientDll, Assert.Single(factory.IsolatedPaths));
        Assert.Equal(0, factory.SharedCalls);
        Assert.Equal(1, factory.DisposeCalls);
    }

    /// <summary>Session is disposed when native build throws and no map is serialized.</summary>
    [Fact]
    public async Task TestBuildAssetsDisposesSessionWhenEngineThrows()
    {
        using var directory = new TemporaryDirectory();
        var asset = await CreateAsset(directory, "id", "asset.mat", new byte[] { 1 });
        var expected = new InvalidOperationException("engine failure");
        var engine = new TestEngineApi { ExceptionToThrow = expected };
        engine.Outputs[asset.FullPath] = new byte[] { 2 };
        var factory = new TestEngineSessionFactory(engine);
        var serializer = new TestBinarySerializer();
        var builder = CreateBuilder(new[] { asset }, factory, serializer);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAssets(new BuildExecutionContext(directory.GetPath("build"))));

        Assert.Same(expected, actual);
        Assert.Equal(1, factory.DisposeCalls);
        Assert.Null(serializer.SerializedMap);
    }

    /// <summary>Creates builder with real registry and cache pipeline.</summary>
    private static AssetBuilder CreateBuilder(IEnumerable<AssetInfo> assets, TestEngineSessionFactory factory, TestBinarySerializer serializer)
    {
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(assets);
        var cache = new AssetBuildCacheService(new TestLogger<AssetBuildCacheService>(), new TestEngineSettingsProvider());
        var pipeline = new AssetBuildCachePipeline(new TestLogger<AssetBuildCachePipeline>(), cache);
        return new AssetBuilder(serializer, factory, registry, pipeline, new TestLogger<AssetBuilder>());
    }

    /// <summary>Creates source asset containing known bytes.</summary>
    private static async Task<AssetInfo> CreateAsset(TemporaryDirectory directory, string id, string name, byte[] bytes)
    {
        var path = directory.GetPath("Project", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
        return new AssetInfo(new AssetMeta(id), path);
    }
}
