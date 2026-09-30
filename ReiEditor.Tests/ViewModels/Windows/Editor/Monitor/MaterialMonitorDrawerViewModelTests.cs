using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies material drawer loading, disposal, restored values and debounce release with bounded waits.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class MaterialMonitorDrawerViewModelTests
{
    /// <summary>
    /// Returns one preloaded material and rejects unrelated asset operations.
    /// </summary>
    private sealed class TestAssetsService(Material material) : IAssetsService
    {
        public ReiEditor.Utils.Common.Observable<bool> Saving { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => Saving;
        public Task<T?> Load<T>(string assetId) where T : Asset => Task.FromResult(material as T);
        public Task<T?> LoadFrom<T>(string projectPath) where T : Asset => throw new NotSupportedException();
        public Task<T?> Load<T>(AssetInfo assetInfo) where T : Asset => throw new NotSupportedException();
        public void Unload(string assetId) => throw new NotSupportedException();
        public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions) => throw new NotSupportedException();
        public Task SaveProject() => throw new NotSupportedException();
    }

    /// <summary>
    /// Supplies one scalar shader and the no-shader branch for inspector lifecycle checks.
    /// </summary>
    private sealed class TestShaderRegistry : IShaderRegistry
    {
        public IReadOnlyDictionary<string, Shader> Shaders { get; } = CreateShaders();
        private static IReadOnlyDictionary<string, Shader> CreateShaders()
        {
            var shader = new Shader();
            shader.SetUniforms([new ShaderUniformInfo("strength", "float", ShaderUniformType.Float)]);
            return new Dictionary<string, Shader> { ["shader-id"] = shader };
        }
        public bool TryGetById(string assetId, [NotNullWhen(true)] out Shader? shader)
        {
            return Shaders.TryGetValue(assetId, out shader);
        }

        public Task RefreshShaders() => throw new NotSupportedException();
    }

    /// <summary>
    /// Records immediate runtime writes performed during disposal.
    /// </summary>
    private sealed class TestAssetRuntimeSyncService : IAssetRuntimeSyncService
    {
        public List<(string Id, string Json)> Writes { get; } = [];
        public string RuntimeJson { get; set; } = "";
        public bool TryGetAssetData(string assetId, out string jsonData)
        {
            jsonData = RuntimeJson;
            return !string.IsNullOrEmpty(jsonData);
        }

        public bool TrySetAssetData(string assetId, string jsonData)
        {
            Writes.Add((assetId, jsonData));
            return true;
        }

        public bool TryPatchAssetData(string assetId, string jsonPatch) => throw new NotSupportedException();
    }

    /// <summary>
    /// Supplies material asset identity without project browser.
    /// </summary>
    private sealed class TestMaterialSelectable : IAssetSelectable
    {
        public string AssetId => "material-id";
        public string AssetName => "Material";
        public string AssetPath => "C:/Assets/material.rmat";
        public bool IsAssetSupportedInMonitor => true;
        public void Select() { }
        public void Deselect() { }
    }

    /// <summary>
    /// Completed material load hydrates editor state and disposal writes current material once to runtime sync.
    /// </summary>
    [AvaloniaFact]
    public async Task LoadWithoutShaderHydratesStateAndDisposePersistsRuntimeData()
    {
        var material = new Material("");
        material.SetUseDepth(false);
        material.SetSortingOrder(77);
        var runtime = new TestAssetRuntimeSyncService();
        var drawer = new MaterialMonitorDrawerViewModel(
            new TestMaterialSelectable(),
            new TestAssetsService(material),
            null!,
            new TestShaderRegistry(),
            new AssetRegistry(new TestLogger<AssetRegistry>()),
            null!,
            runtime,
            null!);

        try
        {
            // Load returns a completed task; drain its queued UI hydration before inspecting state.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.True(drawer.IsMaterialLoaded);
            Assert.False(drawer.UseDepth);
            Assert.Equal(77, drawer.SortingOrder);
            Assert.False(drawer.HasShaderProperties);
            Assert.Equal("Material shader is not set.", drawer.ShaderPropertiesStatusText);
        }
        finally
        {
            drawer.Dispose();
        }

        var write = Assert.Single(runtime.Writes);
        Assert.Equal("material-id", write.Id);
        Assert.Contains("\"SortingOrder\":77", write.Json);
    }

    /// <summary>Closing a stale inspector must keep disk-restored values instead of replaying its old controls.</summary>
    [AvaloniaFact]
    public async Task DisposePreservesRestoredUniformAndIgnoresStaleEditors()
    {
        var material = new Material("shader-id");
        material.Properties["strength"] = 1f;
        var runtime = new TestAssetRuntimeSyncService();
        var drawer = CreateDrawer(material, runtime);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var editor = Assert.IsType<FloatPropertyViewModel>(Assert.Single(drawer.ShaderProperties));
        editor.Value = 4f;
        Assert.Equal(4f, material.Properties["strength"]);
        material.Properties["strength"] = 1f; // Play/Stop restores this same cached asset from disk.

        drawer.Dispose();
        drawer.Dispose();
        editor.Value = 9f;
        drawer.SortingOrder = 90;

        Assert.Equal(1f, material.Properties["strength"]);
        Assert.Equal(1000, material.SortingOrder);
        Assert.Contains("\"strength\":1.0", Assert.Single(runtime.Writes).Json);
    }

    /// <summary>Rapid changes followed by disposal cancel debounce and flush only the latest value once.</summary>
    [AvaloniaFact]
    public async Task DisposeWithPendingDebounceFlushesOnce()
    {
        var material = new Material("");
        var runtime = new TestAssetRuntimeSyncService();
        var drawer = CreateDrawer(material, runtime);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        drawer.SortingOrder = 41;
        drawer.SortingOrder = 42;
        drawer.Dispose();
        drawer.Dispose();
        Assert.Contains("\"SortingOrder\":42", Assert.Single(runtime.Writes).Json);
    }

    /// <summary>A completed debounce must release its pending marker so subsequent native changes reach the inspector.</summary>
    [AvaloniaFact]
    public async Task DebounceCompletionResumesRuntimePull()
    {
        var material = new Material("");
        var runtime = new TestAssetRuntimeSyncService();
        var drawer = CreateDrawer(material, runtime);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            drawer.SortingOrder = 42;
            await WaitFor(() => runtime.Writes.Count > 0);
            runtime.RuntimeJson = "{\"ShaderAssetId\":\"\",\"UseDepth\":true,\"SortingOrder\":88,\"Properties\":{}}";
            await WaitFor(() => drawer.SortingOrder == 88);
            Assert.Equal(88, material.SortingOrder);
        }
        finally
        {
            drawer.Dispose();
        }
    }

    private static MaterialMonitorDrawerViewModel CreateDrawer(Material material, TestAssetRuntimeSyncService runtime) => new(
        new TestMaterialSelectable(), new TestAssetsService(material), null!, new TestShaderRegistry(),
        new AssetRegistry(new TestLogger<AssetRegistry>()), null!, runtime, null!);

    private static async Task WaitFor(Func<bool> ready)
    {
        var elapsed = Stopwatch.StartNew();
        while (!ready() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(10);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        Assert.True(ready(), "Material synchronization did not complete within five seconds.");
    }
}
