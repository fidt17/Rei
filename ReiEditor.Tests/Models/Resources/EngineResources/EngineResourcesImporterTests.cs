using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Resources.EngineResources;

/// <summary>Verifies engine resource import selects supported trees and copies bytes into an isolated project.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Projects")]
public sealed class EngineResourcesImporterTests
{
    private sealed class TestEngineSettingsProvider(string resources) : IEngineSettingsProvider
    {
        public string GetEngineResourcesDir() => resources;
        public Task InitializeAsync() => throw new NotSupportedException();
        public string GetEnginePath() => throw new NotSupportedException();
        public string GetEngineDebugIncludeDir() => throw new NotSupportedException();
        public string GetEngineReleaseIncludeDir() => throw new NotSupportedException();
        public string GetEngineSourceIncludes() => throw new NotSupportedException();
        public string GetEngineBehavioursDir() => throw new NotSupportedException();
        public string GetEngineVersion() => throw new NotSupportedException();
    }

    /// <summary>Import copies all supported categories recursively, overwrites stale bytes and excludes behaviours.</summary>
    [Fact]
    public async Task CopiesSupportedResourceTreesAndOverwritesExistingFiles()
    {
        using var fixture = new TemporaryProjectFixture();
        var source = fixture.Directory.GetPath("EngineSource");
        var categories = new[] { "fonts", "shaders", "materials", "textures", "meshes" };
        foreach (var category in categories)
        {
            var nested = Path.Combine(source, category, "Nested");
            Directory.CreateDirectory(nested);
            await File.WriteAllTextAsync(Path.Combine(nested, "asset.bin"), category);
        }
        Directory.CreateDirectory(Path.Combine(source, "behaviours"));
        await File.WriteAllTextAsync(Path.Combine(source, "behaviours", "Excluded.h"), "excluded");
        var destination = fixture.Resources.GetProjectPath("Engine Resources");
        await fixture.Resources.Write("stale", Path.Combine(destination, "fonts", "Nested", "asset.bin"));
        var service = new EngineResourcesImporter(new TestLogger<EngineResourcesImporter>(), new TestEngineSettingsProvider(source), fixture.Resources);

        await service.Import();

        foreach (var category in categories)
        {
            Assert.Equal(category, await File.ReadAllTextAsync(Path.Combine(destination, category, "Nested", "asset.bin")));
            Assert.Equal(category, await File.ReadAllTextAsync(Path.Combine(source, category, "Nested", "asset.bin")));
        }
        Assert.False(Directory.Exists(Path.Combine(destination, "behaviours")));
        Assert.Equal(categories.Length, Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length);
    }

    /// <summary>A missing engine resource category propagates an IO failure instead of reporting successful import.</summary>
    [Fact]
    public async Task MissingSourcePropagatesFailure()
    {
        using var fixture = new TemporaryProjectFixture();
        var logger = new TestLogger<EngineResourcesImporter>();
        var service = new EngineResourcesImporter(logger, new TestEngineSettingsProvider(fixture.Directory.GetPath("MissingEngine")), fixture.Resources);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.Import());

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("importing complete"));
    }
}
