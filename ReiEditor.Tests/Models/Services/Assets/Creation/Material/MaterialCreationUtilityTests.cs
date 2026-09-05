using ReiEditor.Models.Services.Assets.Creation.Material;
using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Creation.Material;

using RenderShader = ReiEditor.Models.Services.Render.Shader;
using RenderMaterial = ReiEditor.Models.Services.Render.Material;

/// <summary>
/// Verifies material validation and project-relative creation payload.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Assets")]
public sealed class MaterialCreationUtilityTests
{
    /// <summary>Captures material create request.</summary>
    private sealed class TestAssetCreator : IAssetCreator
    {
        public bool Result { get; init; } = true;
        public Asset? CreatedAsset { get; private set; }
        public string? ProjectPath { get; private set; }

        /// <summary>Rejects unused identity allocation.</summary>
        public string AllocateAssetId() => throw new NotSupportedException();

        /// <summary>Captures material and relative path.</summary>
        public Task<bool> Create(Asset asset, string projectPath) { CreatedAsset = asset; ProjectPath = projectPath; return Task.FromResult(Result); }

        /// <summary>Captures identified material and relative path.</summary>
        public Task<bool> Create(Asset asset, string id, string projectPath) => throw new NotSupportedException();
    }

    /// <summary>Controls material-name uniqueness.</summary>
    private sealed class TestAssetRegistry : IAssetRegistry
    {
        public bool IsUnique { get; init; } = true;

        /// <summary>Rejects unused ID lookup.</summary>
        public bool TryGetById(string assetId, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused typed lookup.</summary>
        public bool TryGetByIdAndExtensions(string assetId, IReadOnlyCollection<string> extensions, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused path lookup.</summary>
        public bool TryGetByPath(string fullPath, [NotNullWhen(true)] out AssetInfo? assetInfo) => throw new NotSupportedException();

        /// <summary>Rejects unused loaded lookup.</summary>
        public bool TryGetLoadedAsset(string assetId, [NotNullWhen(true)] out Asset? asset) => throw new NotSupportedException();

        /// <summary>Rejects unused registry replacement.</summary>
        public void UpdateRegistry(IEnumerable<AssetInfo> assets) => throw new NotSupportedException();

        /// <summary>Rejects unused registration.</summary>
        public void RegisterNewAssets(IEnumerable<AssetInfo> assets) => throw new NotSupportedException();

        /// <summary>Rejects unused path update.</summary>
        public void UpdateRegistryPath(string oldPath, string newPath) => throw new NotSupportedException();

        /// <summary>Rejects unused path removal.</summary>
        public void UnregisterByPath(string fullPath) => throw new NotSupportedException();

        /// <summary>Rejects unused subtree removal.</summary>
        public void UnregisterUnderDirectory(string directoryPath) => throw new NotSupportedException();

        /// <summary>Rejects unused dirty-asset enumeration.</summary>
        public IEnumerable<Asset> GetDirtyAssets() => throw new NotSupportedException();

        /// <summary>Rejects unused loaded-info enumeration.</summary>
        public IEnumerable<AssetInfo> GetLoadedAssetInfos() => throw new NotSupportedException();

        /// <summary>Rejects unused asset enumeration.</summary>
        public IEnumerable<AssetInfo> GetAllAssets() => throw new NotSupportedException();

        /// <summary>Rejects unused extension enumeration.</summary>
        public IEnumerable<AssetInfo> GetAllAssetsByExtensions(IReadOnlyCollection<string> extensions) => throw new NotSupportedException();

        /// <summary>Returns configured uniqueness.</summary>
        public bool IsUniqueAssetName(string assetName, string assetExtension) => IsUnique;

        /// <summary>Rejects unused loaded-asset addition.</summary>
        public void AddToLoadedAssets(AssetInfo assetInfo, Asset asset) => throw new NotSupportedException();

        /// <summary>Rejects unused loaded-asset removal.</summary>
        public void RemoveFromLoadedAssets(AssetInfo assetInfo) => throw new NotSupportedException();
    }

    /// <summary>Returns configured shader by matching identity.</summary>
    private sealed class TestShaderRegistry(RenderShader? shader) : IShaderRegistry
    {
        public IReadOnlyDictionary<string, RenderShader> Shaders { get; } = shader == null
            ? new Dictionary<string, RenderShader>()
            : new Dictionary<string, RenderShader> { [shader.AssetId] = shader };

        /// <summary>Resolves configured shader by identity.</summary>
        public bool TryGetById(string assetId, [NotNullWhen(true)] out RenderShader? resolved)
        {
            return Shaders.TryGetValue(assetId, out resolved);
        }

        /// <summary>Rejects unused shader refresh.</summary>
        public Task RefreshShaders() => throw new NotSupportedException();
    }


    /// <summary>
    /// Valid creation passes selected shader identity and project-relative material path to asset creator.
    /// </summary>
    [Fact]
    public async Task CreateMaterialUsesShaderAndProjectRelativePath()
    {
        using var project = new TemporaryProjectFixture();
        var materialsDirectory = project.Resources.GetProjectPath("Materials");
        Directory.CreateDirectory(materialsDirectory);
        var shader = new RenderShader();
        shader.SetAssetInfo(new AssetInfo(new AssetMeta("shader-id"), project.Directory.GetPath("Lit.rshader")));
        var creator = new TestAssetCreator();
        var utility = new MaterialCreationUtility(project.Resources, creator, new TestAssetRegistry(), new TestShaderRegistry(shader), new TestLogger<MaterialCreationUtility>());

        var result = await utility.CreateMaterialAsync(new MaterialCreationSettings(materialsDirectory, "Player Material", "shader-id"));

        Assert.True(result);
        var material = Assert.IsType<RenderMaterial>(creator.CreatedAsset);
        Assert.Equal("shader-id", material.ShaderAssetId);
        Assert.Equal(Path.Combine("Materials", "Player Material.mat"), creator.ProjectPath);
    }

    /// <summary>
    /// Missing shader, duplicate name, invalid name, and outside-project target reject asset creation.
    /// </summary>
    [Theory]
    [InlineData("MissingShader")]
    [InlineData("Duplicate")]
    [InlineData("Invalid-Name")]
    [InlineData("OutsideProject")]
    public async Task InvalidInputsDoNotCreateMaterial(string scenario)
    {
        using var project = new TemporaryProjectFixture();
        using var outside = new TemporaryDirectory();
        var target = scenario == "OutsideProject" ? outside.RootPath : project.Resources.GetProjectPath();
        if (scenario != "OutsideProject") Directory.CreateDirectory(target);
        var name = scenario == "Invalid-Name" ? "Bad/Name" : "Material";
        var creator = new TestAssetCreator();
        var registry = new TestAssetRegistry { IsUnique = scenario != "Duplicate" };
        var shaderRegistry = new TestShaderRegistry(scenario == "MissingShader" ? null : CreateShader(project));
        var utility = new MaterialCreationUtility(project.Resources, creator, registry, shaderRegistry, new TestLogger<MaterialCreationUtility>());

        var result = await utility.CreateMaterialAsync(new MaterialCreationSettings(target, name, "shader-id"));

        Assert.False(result);
        Assert.Null(creator.CreatedAsset);
    }

    /// <summary>
    /// Asset-creator rejection is returned as false with attempted semantic payload retained for inspection.
    /// </summary>
    [Fact]
    public async Task AssetCreatorFailureReturnsFalse()
    {
        using var project = new TemporaryProjectFixture();
        var creator = new TestAssetCreator { Result = false };
        var projectDirectory = project.Resources.GetProjectPath();
        Directory.CreateDirectory(projectDirectory);
        var utility = new MaterialCreationUtility(project.Resources, creator, new TestAssetRegistry(), new TestShaderRegistry(CreateShader(project)), new TestLogger<MaterialCreationUtility>());

        var result = await utility.CreateMaterialAsync(new MaterialCreationSettings(projectDirectory, "Material", "shader-id"));

        Assert.False(result);
        Assert.IsType<RenderMaterial>(creator.CreatedAsset);
        Assert.Equal("Material.mat", creator.ProjectPath);
    }

    /// <summary>Creates shader carrying expected registry asset identity.</summary>
    private static RenderShader CreateShader(TemporaryProjectFixture project)
    {
        var shader = new RenderShader();
        shader.SetAssetInfo(new AssetInfo(new AssetMeta("shader-id"), project.Directory.GetPath("Lit.rshader")));
        return shader;
    }

}
