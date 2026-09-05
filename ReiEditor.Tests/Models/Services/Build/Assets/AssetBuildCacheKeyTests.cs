using ReiEditor.Models.Services.Build.Assets;

namespace ReiEditor.Tests.Models.Services.Build.Assets;

/// <summary>Verifies stable asset cache key construction.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class AssetBuildCacheKeyTests
{
    /// <summary>Cache key combines exact engine version with current builder version.</summary>
    [Fact]
    public void TestCreateCombinesEngineAndBuilderVersions()
    {
        Assert.Equal($"engine-42|{AssetBuildVersions.BUILDER_VERSION}", AssetBuildCacheKey.Create("engine-42"));
    }

    /// <summary>Null engine version is rejected before a key can be persisted.</summary>
    [Fact]
    public void TestCreateRejectsNullEngineVersion()
    {
        Assert.Throws<ArgumentNullException>(() => AssetBuildCacheKey.Create(null!));
    }
}
