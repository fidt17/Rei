using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Shaders;

/// <summary>
/// Verifies shader registry refresh, stale removal, and per-file failure isolation.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Shaders")]
public sealed class ShaderRegistryTests
{
    /// <summary>
    /// Supplies deterministic uniforms and can reject selected shader source.
    /// </summary>
    private sealed class TestShaderUniformParser : IShaderUniformParser
    {
        public string? SourceToReject { get; init; }

        public IReadOnlyList<ShaderUniformInfo> ParseUniforms(string source)
        {
            if (source == SourceToReject) throw new FormatException("Rejected shader source");
            return new[] { new ShaderUniformInfo(source, "float", ShaderUniformType.Float) };
        }
    }

    /// <summary>
    /// Refresh creates shaders with asset identity, filename, and parsed uniforms.
    /// </summary>
    [Fact]
    public async Task RefreshCreatesShaderFromRegisteredFile()
    {
        using var fixture = new TemporaryProjectFixture();
        var path = fixture.Directory.GetPath("Project", "Shaders", "Surface.rshader");
        await fixture.Resources.Write("exposure", path);
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.RegisterNewAssets(new[] { CreateInfo("shader", path) });
        var registry = new ShaderRegistry(assets, new TestShaderUniformParser(), new TestLogger<ShaderRegistry>());

        await registry.RefreshShaders();

        Assert.True(registry.TryGetById("shader", out var shader));
        Assert.Equal("shader", shader.AssetId);
        Assert.Equal(path, shader.FullPath);
        Assert.Equal("Surface", shader.Name);
        Assert.Equal("exposure", Assert.Single(shader.Uniforms).Name);
    }

    /// <summary>
    /// Repeated refresh removes stale shaders and replaces updated shader data.
    /// </summary>
    [Fact]
    public async Task RefreshReplacesPublishedRegistry()
    {
        using var fixture = new TemporaryProjectFixture();
        var firstPath = fixture.Directory.GetPath("Project", "Shaders", "First.rshader");
        var secondPath = fixture.Directory.GetPath("Project", "Shaders", "Second.rshader");
        await fixture.Resources.Write("first", firstPath);
        await fixture.Resources.Write("second", secondPath);
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.UpdateRegistry(new[] { CreateInfo("first", firstPath) });
        var registry = new ShaderRegistry(assets, new TestShaderUniformParser(), new TestLogger<ShaderRegistry>());
        await registry.RefreshShaders();
        var original = registry.Shaders["first"];

        assets.UpdateRegistry(new[] { CreateInfo("second", secondPath) });
        await registry.RefreshShaders();

        Assert.False(registry.TryGetById("first", out _));
        Assert.True(registry.TryGetById("second", out var replacement));
        Assert.NotSame(original, replacement);
        Assert.Equal("second", Assert.Single(replacement.Uniforms).Name);
    }

    /// <summary>
    /// Missing and malformed shaders are skipped without blocking valid files.
    /// </summary>
    [Fact]
    public async Task RefreshSkipsMissingAndRejectedFilesAndContinues()
    {
        using var fixture = new TemporaryProjectFixture();
        var validPath = fixture.Directory.GetPath("Project", "Shaders", "Valid.rshader");
        var rejectedPath = fixture.Directory.GetPath("Project", "Shaders", "Rejected.rshader");
        var missingPath = fixture.Directory.GetPath("Project", "Shaders", "Missing.rshader");
        await fixture.Resources.Write("valid", validPath);
        await fixture.Resources.Write("rejected", rejectedPath);
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.UpdateRegistry(new[]
        {
            CreateInfo("rejected", rejectedPath),
            CreateInfo("missing", missingPath),
            CreateInfo("valid", validPath)
        });
        var logger = new TestLogger<ShaderRegistry>();
        var registry = new ShaderRegistry(assets, new TestShaderUniformParser { SourceToReject = "rejected" }, logger);

        await registry.RefreshShaders();

        Assert.Equal("valid", Assert.Single(registry.Shaders).Key);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevelEnum.Error && entry.Message.Contains(rejectedPath));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains(missingPath));
    }

    private static AssetInfo CreateInfo(string id, string path) => new(new AssetMeta(id), path);
}
