using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Build.Assets.Cache;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.Assets.Cache;

/// <summary>Verifies cache hashing, manifest persistence, entry validation, and safe pruning.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class AssetBuildCacheServiceTests
{
    /// <summary>Supplies deterministic engine version for persisted cache compatibility.</summary>
    private sealed class TestEngineSettingsProvider(string version) : IEngineSettingsProvider
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

    /// <summary>SHA-256 uses lowercase hexadecimal over exact file bytes.</summary>
    [Fact]
    public async Task TestComputeContentHashMatchesKnownSha256()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("asset.bin");
        await File.WriteAllBytesAsync(path, "abc"u8.ToArray());

        var hash = CreateService("engine").ComputeContentHash(path);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    /// <summary>Manifest round trip preserves key and entries and uses fixed manifest name.</summary>
    [Fact]
    public void TestManifestRoundTripPreservesEntries()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService("engine-a");
        var manifest = service.LoadOrCreateManifest(directory.RootPath);
        var asset = CreateAsset(directory, "id", "asset.mat");
        service.AddEntry(manifest, service.CreateEntry(asset, "hash", "id_hash.cache", 7));

        service.SaveManifest(directory.RootPath, manifest);
        var loaded = service.LoadOrCreateManifest(directory.RootPath);

        Assert.Equal(directory.GetPath("asset-cache.json"), service.GetManifestPath(directory.RootPath));
        Assert.Equal($"engine-a|{AssetBuildVersions.BUILDER_VERSION}", loaded.CacheKey);
        var entry = Assert.Single(loaded.Entries).Value;
        Assert.Equal(("id", asset.FullPath, "hash", "id_hash.cache", 7L),
            (entry.AssetId, entry.AssetPath, entry.ContentHash, entry.CacheFileName, entry.CacheSize));
        Assert.Equal("id_hash.cache", service.GetCacheFileName(asset, "hash"));
    }

    /// <summary>Malformed, null, and incompatible manifests produce fresh compatible state.</summary>
    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"CacheKey\":\"old|1\",\"Entries\":{}}")]
    public async Task TestLoadOrCreateManifestRecoversInvalidOrVersionChangedState(string json)
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.GetPath("asset-cache.json"), json);

        var manifest = CreateService("engine-new").LoadOrCreateManifest(directory.RootPath);

        Assert.Equal($"engine-new|{AssetBuildVersions.BUILDER_VERSION}", manifest.CacheKey);
        Assert.Empty(manifest.Entries);
    }

    /// <summary>Matching path, hash, and size returns persisted cache entry and file path.</summary>
    [Fact]
    public async Task TestTryGetCacheEntryReturnsValidatedHit()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService("engine");
        var asset = CreateAsset(directory, "id", "asset.mat");
        var cachePath = directory.GetPath("id_hash.cache");
        await File.WriteAllBytesAsync(cachePath, new byte[] { 1, 2, 3 });
        var manifest = new AssetBuildCacheManifest();
        service.AddEntry(manifest, service.CreateEntry(asset, "hash", "id_hash.cache", 3));

        var hit = service.TryGetCacheEntry(directory.RootPath, manifest, asset, "hash", out var entry, out var resolvedPath);

        Assert.True(hit);
        Assert.Same(manifest.Entries["id"], entry);
        Assert.Equal(cachePath, resolvedPath);
    }

    /// <summary>Changed hash or source path misses without destroying otherwise reusable entry.</summary>
    [Fact]
    public async Task TestTryGetCacheEntryRejectsHashAndPathMismatch()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService("engine");
        var asset = CreateAsset(directory, "id", "asset.mat");
        await File.WriteAllBytesAsync(directory.GetPath("id_hash.cache"), new byte[] { 1 });
        var manifest = new AssetBuildCacheManifest();
        service.AddEntry(manifest, service.CreateEntry(asset, "hash", "id_hash.cache", 1));

        Assert.False(service.TryGetCacheEntry(directory.RootPath, manifest, asset, "changed", out _, out _));
        var moved = new AssetInfo(asset.Meta, directory.GetPath("moved.mat"));
        Assert.False(service.TryGetCacheEntry(directory.RootPath, manifest, moved, "hash", out _, out _));
        Assert.True(manifest.Entries.ContainsKey("id"));
    }

    /// <summary>Missing cache file removes stale manifest entry.</summary>
    [Fact]
    public void TestTryGetCacheEntryRemovesEntryForMissingFile()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService("engine");
        var asset = CreateAsset(directory, "id", "asset.mat");
        var manifest = new AssetBuildCacheManifest();
        service.AddEntry(manifest, service.CreateEntry(asset, "hash", "missing.cache", 3));

        Assert.False(service.TryGetCacheEntry(directory.RootPath, manifest, asset, "hash", out _, out _));
        Assert.Empty(manifest.Entries);
    }

    /// <summary>Wrong-sized cache file is deleted and removed from manifest.</summary>
    [Fact]
    public async Task TestTryGetCacheEntryDeletesWrongSizedFile()
    {
        using var directory = new TemporaryDirectory();
        var service = CreateService("engine");
        var asset = CreateAsset(directory, "id", "asset.mat");
        var cachePath = directory.GetPath("bad.cache");
        await File.WriteAllBytesAsync(cachePath, new byte[] { 1, 2 });
        var manifest = new AssetBuildCacheManifest();
        service.AddEntry(manifest, service.CreateEntry(asset, "hash", "bad.cache", 9));

        Assert.False(service.TryGetCacheEntry(directory.RootPath, manifest, asset, "hash", out _, out _));
        Assert.False(File.Exists(cachePath));
        Assert.Empty(manifest.Entries);
    }

    /// <summary>Pruning deletes only unreferenced top-level cache files.</summary>
    [Fact]
    public async Task TestPruneUnusedCacheFilesKeepsReferencedNestedAndOtherFiles()
    {
        using var directory = new TemporaryDirectory();
        var nested = directory.GetPath("nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllBytesAsync(directory.GetPath("used.cache"), new byte[] { 1 });
        await File.WriteAllBytesAsync(directory.GetPath("unused.cache"), new byte[] { 2 });
        await File.WriteAllBytesAsync(directory.GetPath("other.bin"), new byte[] { 3 });
        await File.WriteAllBytesAsync(Path.Combine(nested, "nested.cache"), new byte[] { 4 });
        var manifest = new AssetBuildCacheManifest();
        manifest.Entries["id"] = new AssetBuildCacheManifest.AssetBuildCacheEntry { CacheFileName = "used.cache" };

        CreateService("engine").PruneUnusedCacheFiles(directory.RootPath, manifest);

        Assert.True(File.Exists(directory.GetPath("used.cache")));
        Assert.False(File.Exists(directory.GetPath("unused.cache")));
        Assert.True(File.Exists(directory.GetPath("other.bin")));
        Assert.True(File.Exists(Path.Combine(nested, "nested.cache")));
    }

    /// <summary>Creates cache service with deterministic version and in-memory logger.</summary>
    private static AssetBuildCacheService CreateService(string version)
        => new(new TestLogger<AssetBuildCacheService>(), new TestEngineSettingsProvider(version));

    /// <summary>Creates asset metadata and path under isolated directory.</summary>
    private static AssetInfo CreateAsset(TemporaryDirectory directory, string id, string fileName)
        => new(new AssetMeta(id), directory.GetPath(fileName));
}
