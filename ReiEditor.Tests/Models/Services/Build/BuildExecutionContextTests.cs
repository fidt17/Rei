using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Build;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies live and custom build execution paths.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class BuildExecutionContextTests
{
    /// <summary>Live context derives bin, resources, and default cache paths from project root.</summary>
    [Fact]
    public void TestCreateLiveDerivesExpectedPaths()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Rei Context", Guid.NewGuid().ToString("N")));

        var context = BuildExecutionContext.CreateLive(root);

        Assert.Equal(Path.Combine(root, ResourceConstants.BIN_DIR_NAME), context.BuildFolder);
        Assert.Equal(Path.Combine(root, ResourceConstants.BIN_DIR_NAME, ResourceConstants.RESOURCES_DIR_NAME), context.ResourcesDirectoryPath);
        Assert.Equal(Path.Combine(root, ResourceConstants.BIN_DIR_NAME, ResourceConstants.RESOURCES_DIR_NAME, "Cache"), context.AssetCacheDirectoryPath);
    }

    /// <summary>Custom cache directory overrides derived resources cache without changing build paths.</summary>
    [Fact]
    public void TestCustomAssetCacheDirectoryOverridesDefault()
    {
        var context = new BuildExecutionContext("build", AssetCacheDirectory: "shared-cache");

        Assert.Equal("shared-cache", context.AssetCacheDirectoryPath);
        Assert.Equal(Path.Combine("build", ResourceConstants.RESOURCES_DIR_NAME), context.ResourcesDirectoryPath);
    }

    /// <summary>Record equality includes every optional execution override.</summary>
    [Fact]
    public void TestEqualityIncludesOptionalOverrides()
    {
        var first = new BuildExecutionContext("build", "out", "client.dll", "cache");
        var same = new BuildExecutionContext("build", "out", "client.dll", "cache");
        var different = new BuildExecutionContext("build", "out", "other.dll", "cache");

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }
}
