using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Tests.Models.Services.Assets;

/// <summary>
/// Verifies monitor support classification for materials, textures, and text previews.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class AssetMonitorSupportUtilityTests
{
    /// <summary>
    /// Material and texture extensions are supported in monitor regardless of extension case.
    /// </summary>
    [Theory]
    [InlineData("surface.mat")]
    [InlineData("surface.MAT")]
    [InlineData("image.png")]
    [InlineData("image.JPG")]
    public void MonitorSupportsMaterialAndTextureFiles(string path)
    {
        Assert.True(AssetMonitorSupportUtility.IsAssetSupportedInMonitor(path, isDirectory: false));
    }

    /// <summary>
    /// Material and texture classifiers accept only their own file extensions.
    /// </summary>
    [Fact]
    public void SpecializedPreviewClassifiersRemainDistinct()
    {
        Assert.True(AssetMonitorSupportUtility.IsMaterialAsset("surface.MAT", isDirectory: false));
        Assert.False(AssetMonitorSupportUtility.IsMaterialAsset("image.png", isDirectory: false));
        Assert.True(AssetMonitorSupportUtility.IsTexturePreviewAsset("image.JPG", isDirectory: false));
        Assert.False(AssetMonitorSupportUtility.IsTexturePreviewAsset("surface.mat", isDirectory: false));
    }

    /// <summary>
    /// Header, C++, and shader files support text preview but not material or texture monitor.
    /// </summary>
    [Theory]
    [InlineData("Behaviour.H")]
    [InlineData("Behaviour.cpp")]
    [InlineData("Surface.RSHADER")]
    public void TextPreviewSupportsSourceAndShaderFiles(string path)
    {
        Assert.True(AssetMonitorSupportUtility.IsTextPreviewAsset(path, isDirectory: false));
        Assert.False(AssetMonitorSupportUtility.IsAssetSupportedInMonitor(path, isDirectory: false));
    }

    /// <summary>
    /// Directories and blank or unsupported paths are rejected by every monitor classifier.
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("notes.txt", false)]
    [InlineData("surface.mat", true)]
    [InlineData("image.png", true)]
    [InlineData("source.cpp", true)]
    public void InvalidMonitorInputsAreRejected(string path, bool isDirectory)
    {
        Assert.False(AssetMonitorSupportUtility.IsAssetSupportedInMonitor(path, isDirectory));
        Assert.False(AssetMonitorSupportUtility.IsMaterialAsset(path, isDirectory));
        Assert.False(AssetMonitorSupportUtility.IsTexturePreviewAsset(path, isDirectory));
        Assert.False(AssetMonitorSupportUtility.IsTextPreviewAsset(path, isDirectory));
    }
}
