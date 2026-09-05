using System.Diagnostics.CodeAnalysis;
using ReiEditor.Models.EditorApp.AssetCreation.Behaviour;
using ReiEditor.Models.EditorApp.AssetCreation.Material;
using ReiEditor.Models.EditorApp.AssetCreation.Shader;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Creation.Behaviour;
using ReiEditor.Models.Services.Assets.Creation.Material;
using ReiEditor.Models.Services.Assets.Creation.Shader;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Render;
using ReiEditor.ViewModels.Windows.Editor.Project.AssetCreation;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project.AssetCreation;

/// <summary>Verifies shader and behaviour form requests, outcomes, and cancellation.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ViewModels")]
public sealed class AssetCreationWindowViewModelTests
{
    /// <summary>Provides shader entries; asset registry calls are unnecessary in picker entry mode.</summary>
    private sealed class TestAssetRegistry : IAssetRegistry
    {
        public bool TryGetById(string assetId, [NotNullWhen(true)] out AssetInfo? assetInfo) { assetInfo = null; return false; }
        public bool TryGetByIdAndExtensions(string assetId, IReadOnlyCollection<string> extensions, [NotNullWhen(true)] out AssetInfo? assetInfo) { assetInfo = null; return false; }
        public bool TryGetByPath(string fullPath, [NotNullWhen(true)] out AssetInfo? assetInfo) { assetInfo = null; return false; }
        public bool TryGetLoadedAsset(string assetId, [NotNullWhen(true)] out Asset? asset) { asset = null; return false; }
        public void UpdateRegistry(IEnumerable<AssetInfo> assets) { }
        public void RegisterNewAssets(IEnumerable<AssetInfo> assets) { }
        public void UpdateRegistryPath(string oldPath, string newPath) { }
        public void UnregisterByPath(string fullPath) { }
        public void UnregisterUnderDirectory(string directoryPath) { }
        public IEnumerable<Asset> GetDirtyAssets() => Array.Empty<Asset>();
        public IEnumerable<AssetInfo> GetLoadedAssetInfos() => Array.Empty<AssetInfo>();
        public IEnumerable<AssetInfo> GetAllAssets() => Array.Empty<AssetInfo>();
        public IEnumerable<AssetInfo> GetAllAssetsByExtensions(IReadOnlyCollection<string> extensions) => Array.Empty<AssetInfo>();
        public bool IsUniqueAssetName(string assetName, string assetExtension) => true;
        public void AddToLoadedAssets(AssetInfo assetInfo, Asset asset) { }
        public void RemoveFromLoadedAssets(AssetInfo assetInfo) { }
    }

    /// <summary>Provides a fixed shader list to material picker.</summary>
    private sealed class TestShaderRegistry(IReadOnlyDictionary<string, Shader> shaders) : IShaderRegistry
    {
        public IReadOnlyDictionary<string, Shader> Shaders => shaders;
        public bool TryGetById(string assetId, [NotNullWhen(true)] out Shader? shader) => shaders.TryGetValue(assetId, out shader);
        public Task RefreshShaders() => Task.CompletedTask;
    }

    /// <summary>Records shader settings and supplies requested creation result.</summary>
    private sealed class TestShaderCreationUtility(bool result) : IShaderCreationUtility
    {
        public ShaderCreationSettings? Settings { get; private set; }
        public Task<bool> CreateShaderAsync(ShaderCreationSettings settings)
        {
            Settings = settings;
            return Task.FromResult(result);
        }
    }

    /// <summary>Records behaviour settings and supplies requested creation result.</summary>
    private sealed class TestBehaviourCreationUtility(bool result) : IBehaviourCreationUtility
    {
        public BehaviourCreationSettings? Settings { get; private set; }
        public Task<bool> CreateBehaviourAsync(BehaviourCreationSettings settings)
        {
            Settings = settings;
            return Task.FromResult(result);
        }
    }

    /// <summary>Records material settings and supplies requested creation result.</summary>
    private sealed class TestMaterialCreationUtility(bool result) : IMaterialCreationUtility
    {
        public MaterialCreationSettings? Settings { get; private set; }
        public Task<bool> CreateMaterialAsync(MaterialCreationSettings settings)
        {
            Settings = settings;
            return Task.FromResult(result);
        }
    }

    /// <summary>Records shader form close requests.</summary>
    private sealed class TestShaderWindowService : IShaderCreationWindowService
    {
        public int CloseCount { get; private set; }
        public void OpenShaderCreationWindow(string targetDirectory, Action onCreated) { }
        public void CloseShaderCreationWindow() => CloseCount++;
    }

    /// <summary>Records behaviour form close requests.</summary>
    private sealed class TestBehaviourWindowService : IBehaviourCreationWindowService
    {
        public int CloseCount { get; private set; }
        public void OpenBehaviourCreationWindow(string targetDirectory, Action onCreated) { }
        public void CloseBehaviourCreationWindow() => CloseCount++;
    }

    /// <summary>Records material form close requests.</summary>
    private sealed class TestMaterialWindowService : IMaterialCreationWindowService
    {
        public int CloseCount { get; private set; }
        public void OpenMaterialCreationWindow(string targetDirectory, Action onCreated) { }
        public void CloseMaterialCreationWindow() => CloseCount++;
    }

    /// <summary>Successful shader creation forwards input, invokes callback, and closes form.</summary>
    [Fact]
    public void TestShaderSuccessClosesAndInvokesCreatedCallback()
    {
        var utility = new TestShaderCreationUtility(true);
        var window = new TestShaderWindowService();
        var created = 0;
        var vm = new CreateShaderAssetWindowViewModel("assets", () => created++, window, utility)
        {
            ShaderName = "Water",
        };

        vm.CreateCommand.Execute(null);

        Assert.Equal("assets", utility.Settings?.TargetDirectory);
        Assert.Equal("Water", utility.Settings?.ShaderName);
        Assert.Equal(1, created);
        Assert.Equal(1, window.CloseCount);
        Assert.Empty(vm.ErrorText);
    }

    /// <summary>Rejected shader creation preserves form input and reports failure without closing.</summary>
    [Fact]
    public void TestShaderFailurePreservesInputAndKeepsFormOpen()
    {
        var utility = new TestShaderCreationUtility(false);
        var window = new TestShaderWindowService();
        var vm = new CreateShaderAssetWindowViewModel("assets", () => throw new Xunit.Sdk.XunitException("Unexpected callback"), window, utility)
        {
            ShaderName = "Duplicate",
        };

        vm.CreateCommand.Execute(null);

        Assert.Equal("Duplicate", vm.ShaderName);
        Assert.NotEmpty(vm.ErrorText);
        Assert.Equal(0, window.CloseCount);
    }

    /// <summary>Behaviour creation forwards every override flag and closes after success.</summary>
    [Fact]
    public void TestBehaviourSuccessForwardsOptionsAndCloses()
    {
        var utility = new TestBehaviourCreationUtility(true);
        var window = new TestBehaviourWindowService();
        var vm = new CreateBehaviourAssetWindowViewModel("scripts", () => { }, window, utility)
        {
            BehaviourName = "Player",
            OverrideInit = true,
            OverrideStart = true,
            OverrideUpdate = false,
            OverrideDispose = true,
        };

        vm.CreateCommand.Execute(null);

        Assert.Equal("scripts", utility.Settings?.TargetDirectory);
        Assert.Equal("Player", utility.Settings?.BehaviourName);
        Assert.True(utility.Settings.HasValue);
        Assert.True(utility.Settings.Value.OverrideInit);
        Assert.True(utility.Settings.Value.OverrideStart);
        Assert.False(utility.Settings.Value.OverrideUpdate);
        Assert.True(utility.Settings.Value.OverrideDispose);
        Assert.Equal(1, window.CloseCount);
    }

    /// <summary>Cancel closes shader and behaviour forms without invoking creation utilities.</summary>
    [Fact]
    public void TestCancelClosesFormsWithoutCreatingAssets()
    {
        var shaderUtility = new TestShaderCreationUtility(true);
        var shaderWindow = new TestShaderWindowService();
        var behaviourUtility = new TestBehaviourCreationUtility(true);
        var behaviourWindow = new TestBehaviourWindowService();
        var shader = new CreateShaderAssetWindowViewModel("assets", () => { }, shaderWindow, shaderUtility);
        var behaviour = new CreateBehaviourAssetWindowViewModel("scripts", () => { }, behaviourWindow, behaviourUtility);

        shader.CancelCommand.Execute(null);
        behaviour.CancelCommand.Execute(null);

        Assert.Null(shaderUtility.Settings);
        Assert.Null(behaviourUtility.Settings);
        Assert.Equal(1, shaderWindow.CloseCount);
        Assert.Equal(1, behaviourWindow.CloseCount);
    }

    /// <summary>Material form requires shader selection before calling creation utility.</summary>
    [Fact]
    public void TestMaterialWithoutShaderReportsValidationError()
    {
        var utility = new TestMaterialCreationUtility(true);
        var window = new TestMaterialWindowService();
        using var vm = new CreateMaterialAssetWindowViewModel("materials", () => { }, window, utility, new TestAssetRegistry(), TestCreateShaderRegistry());

        vm.CreateCommand.Execute(null);

        Assert.Null(utility.Settings);
        Assert.NotEmpty(vm.ErrorText);
        Assert.Equal(0, window.CloseCount);
    }

    /// <summary>Selected shader and material name are forwarded, then successful form closes.</summary>
    [Fact]
    public void TestMaterialSuccessForwardsSelectionAndCloses()
    {
        var utility = new TestMaterialCreationUtility(true);
        var window = new TestMaterialWindowService();
        var created = 0;
        using var vm = new CreateMaterialAssetWindowViewModel("materials", () => created++, window, utility, new TestAssetRegistry(), TestCreateShaderRegistry())
        {
            MaterialName = "Player Material",
        };
        vm.ShaderPicker.SearchResults.Single().SelectCommand.Execute(null);

        vm.CreateCommand.Execute(null);

        Assert.Equal("materials", utility.Settings?.TargetDirectory);
        Assert.Equal("Player Material", utility.Settings?.MaterialName);
        Assert.Equal("shader-id", utility.Settings?.ShaderAssetId);
        Assert.Equal(1, created);
        Assert.Equal(1, window.CloseCount);
        Assert.Empty(vm.ErrorText);
    }

    /// <summary>Creates one valid shader picker entry for material form tests.</summary>
    private static TestShaderRegistry TestCreateShaderRegistry()
    {
        var shader = new Shader();
        shader.SetName("Lit");
        shader.SetAssetInfo(new AssetInfo(new ReiEditor.Models.Services.Assets.Meta.AssetMeta("shader-id"), "lit.shader"));
        return new TestShaderRegistry(new Dictionary<string, Shader> { [shader.AssetId] = shader });
    }
}
