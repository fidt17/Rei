using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets;

/// <summary>
/// Verifies asset lookup, registration, loaded-asset caching, and path updates.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class AssetRegistryTests
{
    /// <summary>
    /// ID, path, and case-insensitive extension lookups return registered asset metadata.
    /// </summary>
    [Fact]
    public void RegisteredAssetCanBeFoundByIdPathAndExtension()
    {
        using var fixture = new TemporaryProjectFixture();
        var path = fixture.Directory.GetPath("Project", "Materials", "surface.MAT");
        var info = CreateInfo("material", path);
        var registry = CreateRegistry();
        registry.RegisterNewAssets(new[] { info });

        Assert.True(registry.TryGetById("material", out var byId));
        Assert.Same(info, byId);
        Assert.True(registry.TryGetByPath(path, out var byPath));
        Assert.Same(info, byPath);
        Assert.True(registry.TryGetByIdAndExtensions("material", new[] { ".mat" }, out var byExtension));
        Assert.Same(info, byExtension);
    }

    /// <summary>
    /// Unknown or blank IDs and empty or mismatched extension sets fail without returning metadata.
    /// </summary>
    [Fact]
    public void InvalidLookupInputsReturnFalse()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        registry.RegisterNewAssets(new[] { CreateInfo("material", fixture.Directory.GetPath("surface.mat")) });

        Assert.False(registry.TryGetById("missing", out var missing));
        Assert.Null(missing);
        Assert.False(registry.TryGetByIdAndExtensions("", new[] { ".mat" }, out _));
        Assert.False(registry.TryGetByIdAndExtensions("material", Array.Empty<string>(), out _));
        Assert.False(registry.TryGetByIdAndExtensions("material", new[] { ".scene" }, out _));
    }

    /// <summary>
    /// Incremental registration preserves first duplicate ID and records warning.
    /// </summary>
    [Fact]
    public void RegisterNewAssetsPreservesFirstDuplicateId()
    {
        using var fixture = new TemporaryProjectFixture();
        var logger = new TestLogger<AssetRegistry>();
        var registry = new AssetRegistry(logger);
        var first = CreateInfo("same", fixture.Directory.GetPath("first.mat"));
        var second = CreateInfo("same", fixture.Directory.GetPath("second.mat"));

        registry.RegisterNewAssets(new[] { first, second });

        Assert.True(registry.TryGetById("same", out var stored));
        Assert.Same(first, stored);
        Assert.Single(logger.Entries);
    }

    /// <summary>
    /// Full registry replacement keeps last duplicate ID and drops loaded assets no longer registered.
    /// </summary>
    [Fact]
    public void UpdateRegistryKeepsLastDuplicateAndPrunesLoadedAssets()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        var loadedInfo = CreateInfo("loaded", fixture.Directory.GetPath("loaded.mat"));
        registry.AddToLoadedAssets(loadedInfo, new Material());
        var first = CreateInfo("same", fixture.Directory.GetPath("first.mat"));
        var last = CreateInfo("same", fixture.Directory.GetPath("last.mat"));

        registry.UpdateRegistry(new[] { first, last });

        Assert.True(registry.TryGetById("same", out var stored));
        Assert.Same(last, stored);
        Assert.False(registry.TryGetLoadedAsset("loaded", out _));
    }

    /// <summary>
    /// Loaded cache preserves object identity, assigns asset info, exposes it as dirty, and rejects duplicate load.
    /// </summary>
    [Fact]
    public void LoadedAssetPreservesIdentityAndRejectsDuplicateLoad()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        var info = CreateInfo("material", fixture.Directory.GetPath("surface.mat"));
        var material = new Material();

        registry.AddToLoadedAssets(info, material);

        Assert.True(registry.TryGetLoadedAsset("material", out var loaded));
        Assert.Same(material, loaded);
        Assert.Equal("material", material.AssetId);
        Assert.Equal(info.FullPath, material.FullPath);
        Assert.Same(material, Assert.Single(registry.GetDirtyAssets()));
        Assert.Same(info, Assert.Single(registry.GetLoadedAssetInfos()));
        Assert.Throws<Exception>(() => registry.AddToLoadedAssets(info, new Material()));
    }

    /// <summary>
    /// Removing loaded asset leaves registry metadata available.
    /// </summary>
    [Fact]
    public void RemoveFromLoadedAssetsLeavesRegistryEntry()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        var info = CreateInfo("material", fixture.Directory.GetPath("surface.mat"));
        registry.AddToLoadedAssets(info, new Material());

        registry.RemoveFromLoadedAssets(info);

        Assert.False(registry.TryGetLoadedAsset("material", out _));
        Assert.True(registry.TryGetById("material", out _));
    }

    /// <summary>
    /// Asset snapshots remain stable after registry replacement.
    /// </summary>
    [Fact]
    public void GetAllAssetsReturnsSnapshot()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        var first = CreateInfo("first", fixture.Directory.GetPath("first.mat"));
        var second = CreateInfo("second", fixture.Directory.GetPath("second.mat"));
        registry.UpdateRegistry(new[] { first });
        var snapshot = registry.GetAllAssets();

        registry.UpdateRegistry(new[] { second });

        Assert.Same(first, Assert.Single(snapshot));
        Assert.Same(second, Assert.Single(registry.GetAllAssets()));
    }

    /// <summary>
    /// Directory path update moves descendants while preserving neighboring prefix paths.
    /// </summary>
    [Fact]
    public void UpdateRegistryPathMovesDirectoryTreeWithoutMatchingNeighborPrefix()
    {
        using var fixture = new TemporaryProjectFixture();
        var oldDirectory = fixture.Directory.GetPath("Project", "Art");
        var newDirectory = fixture.Directory.GetPath("Project", "Moved");
        Directory.CreateDirectory(newDirectory);
        var child = CreateInfo("child", Path.Combine(oldDirectory, "Nested", "surface.mat"));
        var neighbor = CreateInfo("neighbor", fixture.Directory.GetPath("Project", "Artwork", "other.mat"));
        var registry = CreateRegistry();
        registry.UpdateRegistry(new[] { child, neighbor });

        registry.UpdateRegistryPath(oldDirectory, newDirectory);

        Assert.True(registry.TryGetById("child", out var moved));
        Assert.Equal(Path.Combine(newDirectory, "Nested", "surface.mat"), moved.FullPath);
        Assert.True(registry.TryGetById("neighbor", out var unchanged));
        Assert.Equal(neighbor.FullPath, unchanged.FullPath);
    }

    /// <summary>
    /// Unregistering directory removes descendants and loaded cache entries while preserving neighboring prefix paths.
    /// </summary>
    [Fact]
    public void UnregisterUnderDirectoryRemovesOnlyTreeAndLoadedCache()
    {
        using var fixture = new TemporaryProjectFixture();
        var directory = fixture.Directory.GetPath("Project", "Art");
        var child = CreateInfo("child", Path.Combine(directory, "surface.mat"));
        var neighbor = CreateInfo("neighbor", fixture.Directory.GetPath("Project", "Artwork", "other.mat"));
        var registry = CreateRegistry();
        registry.AddToLoadedAssets(child, new Material());
        registry.RegisterNewAssets(new[] { neighbor });

        registry.UnregisterUnderDirectory(directory);

        Assert.False(registry.TryGetById("child", out _));
        Assert.False(registry.TryGetLoadedAsset("child", out _));
        Assert.True(registry.TryGetById("neighbor", out _));
    }

    /// <summary>
    /// Extension enumeration is case-insensitive and uniqueness compares filenames without extension case-insensitively.
    /// </summary>
    [Fact]
    public void ExtensionEnumerationAndNameUniquenessIgnoreCase()
    {
        using var fixture = new TemporaryProjectFixture();
        var registry = CreateRegistry();
        var material = CreateInfo("material", fixture.Directory.GetPath("Surface.MAT"));
        var scene = CreateInfo("scene", fixture.Directory.GetPath("Surface.scene"));
        registry.UpdateRegistry(new[] { material, scene });

        Assert.Same(material, Assert.Single(registry.GetAllAssetsByExtensions(new[] { ".mat" })));
        Assert.Empty(registry.GetAllAssetsByExtensions(Array.Empty<string>()));
        Assert.False(registry.IsUniqueAssetName("surface", ".mat"));
        Assert.True(registry.IsUniqueAssetName("other", ".mat"));
    }

    private static AssetRegistry CreateRegistry() => new(new TestLogger<AssetRegistry>());

    private static AssetInfo CreateInfo(string id, string path) => new(new AssetMeta(id), path);
}
