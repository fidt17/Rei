using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.FileSystem;

/// <summary>
/// Verifies production consumers apply asset filtering consistently at filesystem boundaries.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "AssetFiltering")]
public sealed class AssetFilterConsumerTests
{
    /// <summary>
    /// Directory regeneration creates metadata only for eligible assets and never creates metadata chains.
    /// </summary>
    [Fact]
    public async Task RepeatedMetaRegenerationFiltersUppercaseGeneratedFilesWithoutCreatingChains()
    {
        using var fixture = new TemporaryProjectFixture();
        var directory = fixture.Resources.GetProjectPath("Regeneration");
        Directory.CreateDirectory(directory);
        var texturePath = Path.Combine(directory, "Texture.PNG");
        var rejectedPaths = new[]
        {
            Path.Combine(directory, "Source.CPP"),
            Path.Combine(directory, "Existing.META"),
            Path.Combine(directory, "Game.VCXPROJ")
        };
        await File.WriteAllTextAsync(texturePath, "texture");
        foreach (var rejectedPath in rejectedPaths)
        {
            await File.WriteAllTextAsync(rejectedPath, "generated");
        }
        var service = CreateMetaService(fixture);

        await service.RegenerateMetaFilesInDirectory(directory, DefaultMetaFileRegenerationPolicy.Instance);
        await service.RegenerateMetaFilesInDirectory(directory, DefaultMetaFileRegenerationPolicy.Instance);

        Assert.True(File.Exists(texturePath + ".meta"));
        Assert.False(string.IsNullOrWhiteSpace((await fixture.Resources.Load<AssetMeta>(texturePath + ".meta")).AssetId));
        Assert.All(rejectedPaths, path => Assert.False(File.Exists(path + ".meta")));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories),
            path => path.EndsWith(".meta.meta", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Single-file regeneration rejects generated files regardless of extension casing.
    /// </summary>
    [Theory]
    [InlineData("Source.CPP")]
    [InlineData("Existing.META")]
    [InlineData("Game.VCXPROJ")]
    public async Task MetaRegenerationForSingleFileRejectsUppercaseGeneratedExtension(string fileName)
    {
        using var fixture = new TemporaryProjectFixture();
        var assetPath = fixture.Resources.GetProjectPath("Single", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllTextAsync(assetPath, "generated");
        var service = CreateMetaService(fixture);

        await service.RegenerateMetaFileForAsset(assetPath, DefaultMetaFileRegenerationPolicy.Instance);

        Assert.False(File.Exists(assetPath + ".meta"));
    }

    /// <summary>
    /// Search traverses similarly named asset trees while hiding only generated project trees and files.
    /// </summary>
    [Fact]
    public async Task AssetSearchAppliesDirectoryBoundariesAndCaseInsensitiveGeneratedFileFiltering()
    {
        using var fixture = new TemporaryProjectFixture();
        var projectRoot = fixture.Resources.GetProjectPath();
        var visiblePaths = new[]
        {
            Path.Combine(projectRoot, "OtherProject", "Scripts", "bin", "UserAsset.png"),
            Path.Combine(projectRoot, "OrdinaryAsset.png")
        };
        var hiddenPaths = new[]
        {
            Path.Combine(projectRoot, "Scripts", "bin", "BuildAsset.png"),
            Path.Combine(projectRoot, "Scripts", "crash_reports", "CrashAsset.png"),
            Path.Combine(projectRoot, "MetadataAsset.META"),
            Path.Combine(projectRoot, "ProjectAsset.VCXPROJ")
        };
        foreach (var path in visiblePaths.Concat(hiddenPaths))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "asset");
        }

        var results = new AssetSearchService(fixture.Resources).Search("asset");

        Assert.Equal(
            visiblePaths.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase),
            results.Select(result => result.FullPath));
    }

    /// <summary>
    /// Build path filtering distinguishes actual script output trees from matching nested segments elsewhere.
    /// </summary>
    [Fact]
    public void AssetBuildPathFilteringRespectsProjectScriptDirectoryBoundary()
    {
        using var directory = new TemporaryDirectory();
        var visiblePath = directory.GetPath("Project", "OtherProject", "Scripts", "bin", "nested", "texture.png");
        var hiddenPath = directory.GetPath("Project", "Scripts", "bin", "nested", "texture.png");

        Assert.True(AssetBuildPathUtility.ShouldBuildPath(visiblePath));
        Assert.False(AssetBuildPathUtility.ShouldBuildPath(hiddenPath));
    }

    /// <summary>
    /// Creates production metadata service with isolated project resources.
    /// </summary>
    private static MetaFilesService CreateMetaService(TemporaryProjectFixture fixture)
    {
        return new MetaFilesService(fixture.Resources, new JsonSerializer(), new TestLogger<MetaFilesService>());
    }
}
