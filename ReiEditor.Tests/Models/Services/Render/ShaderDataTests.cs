using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Tests.Models.Services.Render;

/// <summary>Verifies shader list replacement, picker mapping, and material shader changes without native rendering.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ShaderData")]
public sealed class ShaderDataTests
{
    private sealed class TestShaderRegistry(IReadOnlyDictionary<string, Shader> shaders) : IShaderRegistry
    {
        public IReadOnlyDictionary<string, Shader> Shaders => shaders;
        public bool TryGetById(string assetId, [NotNullWhen(true)] out Shader? shader) => shaders.TryGetValue(assetId, out shader);
        public Task RefreshShaders() => throw new InvalidOperationException("Unexpected shader refresh.");
    }

    /// <summary>Uniform replacement removes stale entries and filters null inputs while preserving valid entries.</summary>
    [Fact]
    public void ShaderUniformReplacementDropsOldEntries()
    {
        var shader = new Shader();
        shader.SetUniforms(new[] { new ShaderUniformInfo("old", "float", ShaderUniformType.Float) });
        var replacement = new ShaderUniformInfo("albedo", "sampler2D", ShaderUniformType.Texture);

        shader.SetUniforms(new[] { null!, replacement });

        Assert.Same(replacement, Assert.Single(shader.Uniforms));
    }

    /// <summary>Picker entries sort by display name and preserve the corresponding asset IDs and paths.</summary>
    [Fact]
    public void PickerEntriesSortAndPreserveAssetIdentity()
    {
        var z = CreateShader("Zebra", "z-id", "z.rshader");
        var a = CreateShader("Alpha", "a-id", "a.rshader");
        var registry = new TestShaderRegistry(new Dictionary<string, Shader> { [z.AssetId] = z, [a.AssetId] = a });

        var entries = registry.BuildEntries();

        Assert.Equal(new[] { "Alpha", "Zebra" }, entries.Select(entry => entry.Name));
        Assert.Equal(new[] { "a-id", "z-id" }, entries.Select(entry => entry.AssetId));
        Assert.Equal(new[] { "a.rshader", "z.rshader" }, entries.Select(entry => entry.FullPath));
        Assert.Empty(new TestShaderRegistry(new Dictionary<string, Shader>()).BuildEntries());
    }

    /// <summary>Changing a material shader trims its ID but leaves persisted material properties intact.</summary>
    [Fact]
    public void ChangingShaderPreservesMaterialProperties()
    {
        var material = new Material("old");
        material.Properties["strength"] = 2f;

        material.SetShaderAssetId(" new-id ");

        Assert.Equal("new-id", material.ShaderAssetId);
        Assert.Equal(2f, material.Properties["strength"]);
        material.SetShaderAssetId(null!);
        Assert.Equal("", material.ShaderAssetId);
    }

    private static Shader CreateShader(string name, string id, string path)
    {
        var shader = new Shader();
        shader.SetName(name);
        shader.SetAssetInfo(new AssetInfo(new AssetMeta(id), path));
        return shader;
    }
}
