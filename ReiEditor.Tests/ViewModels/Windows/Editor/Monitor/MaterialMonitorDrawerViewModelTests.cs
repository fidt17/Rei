using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Models.Services.Render;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.Views.Controls.TextBoxes;

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
    private sealed class TestShaderRegistry(params ShaderUniformInfo[] additionalUniforms) : IShaderRegistry
    {
        public IReadOnlyDictionary<string, Shader> Shaders { get; } = CreateShaders(additionalUniforms);
        private static IReadOnlyDictionary<string, Shader> CreateShaders(ShaderUniformInfo[] additionalUniforms)
        {
            var first = CreateShader("shader-id", [new ShaderUniformInfo("strength", "float", ShaderUniformType.Float), ..additionalUniforms]);
            var second = CreateShader("second-shader", [new("detail", "float", ShaderUniformType.Float), new("tint", "vec4", ShaderUniformType.Color)]);
            return new Dictionary<string, Shader> { [first.AssetId] = first, [second.AssetId] = second };
        }

        private static Shader CreateShader(string id, ShaderUniformInfo[] uniforms)
        {
            var shader = new Shader();
            shader.SetAssetInfo(new AssetInfo(new AssetMeta(id), $"C:/Assets/{id}.rshader"));
            shader.SetName(id);
            shader.SetUniforms(uniforms);
            return shader;
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
        public bool IsLoadedInRuntime { get; set; } = true;
        public int ReadCount { get; private set; }
        public bool TryGetAssetData(string assetId, out string jsonData)
        {
            ReadCount++;
            jsonData = RuntimeJson;
            return !string.IsNullOrEmpty(jsonData);
        }

        public bool TrySetAssetData(string assetId, string jsonData)
        {
            Writes.Add((assetId, jsonData));
            return IsLoadedInRuntime;
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
    /// Loading and closing the Inspector in read-only use leaves runtime state alone.
    /// </summary>
    [AvaloniaFact]
    public async Task LoadWithoutShaderHydratesStateAndDisposeDoesNotWriteRuntimeData()
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

        Assert.Empty(runtime.Writes);
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
        var material = new Material("shader-id");
        var runtime = new TestAssetRuntimeSyncService();
        var drawer = CreateDrawer(material, runtime);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var strength = Assert.IsType<FloatPropertyViewModel>(Assert.Single(drawer.ShaderProperties));
            drawer.SortingOrder = 42;
            await WaitFor(() => runtime.Writes.Count > 0);
            runtime.RuntimeJson = "{\"ShaderAssetId\":\"shader-id\",\"UseDepth\":true,\"SortingOrder\":88,\"Properties\":{\"strength\":2.5}}";
            await WaitFor(() => drawer.SortingOrder == 88);
            Assert.Equal(88, material.SortingOrder);
            Assert.Same(strength, Assert.Single(drawer.ShaderProperties));
            Assert.Equal(2.5f, strength.Value);
        }
        finally
        {
            drawer.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task ShaderSelectionRestoresExactUniformsAndReplaysEditsOnEitherSide()
    {
        var material = new Material("shader-id");
        material.SetUseDepth(false);
        material.SetSortingOrder(17);
        material.Properties["strength"] = 1.5f;
        material.Properties["tint"] = new JObject { ["r"] = 0.25, ["g"] = 0.5, ["b"] = 0.75, ["a"] = 1.0 };
        var runtime = new TestAssetRuntimeSyncService();
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.AddToLoadedAssets(new AssetInfo(new AssetMeta("material-id"), "C:/Assets/material.rmat"), material);
        var shaders = new TestShaderRegistry(new ShaderUniformInfo("tint", "vec4", ShaderUniformType.Color));
        using var history = new EditorCommandService();
        var command = new SetMaterialPropertyCommand(history, assets, runtime, new TestEngineRunner());
        using var drawer = new MaterialMonitorDrawerViewModel(new TestMaterialSelectable(), new TestAssetsService(material),
            null!, shaders, assets, new AssetTypeMapper(), runtime, null!, command);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        Assert.False(history.CanUndo);
        AssertState("shader-id", "strength", 1.5f);
        Number().Value = 2.5f;
        var beforeSwitch = JObject.FromObject(material.Properties);
        var choice = Assert.Single(drawer.ShaderPicker.SearchResults, item => item.AssetId == "second-shader");
        choice.SelectCommand.Execute(null);
        AssertState("second-shader", "detail", 0f);
        Assert.False(material.Properties.ContainsKey("strength"));
        Number().Value = 4f;

        await history.UndoAsync();
        AssertState("second-shader", "detail", 0f);
        await history.UndoAsync();
        AssertState("shader-id", "strength", 2.5f);
        Assert.True(JToken.DeepEquals(beforeSwitch, JObject.FromObject(material.Properties)));
        await history.UndoAsync();
        AssertState("shader-id", "strength", 1.5f);
        Assert.False(history.CanUndo);

        await history.RedoAsync();
        AssertState("shader-id", "strength", 2.5f);
        await history.RedoAsync();
        AssertState("second-shader", "detail", 0f);
        await history.RedoAsync();
        AssertState("second-shader", "detail", 4f);
        Assert.False(history.CanRedo);

        drawer.ShaderPicker.ClearAsset();
        Assert.Equal("", material.ShaderAssetId);
        Assert.Empty(material.Properties);
        Assert.Empty(drawer.ShaderProperties);
        await history.UndoAsync();
        AssertState("second-shader", "detail", 4f);

        FloatPropertyViewModel Number() => Assert.Single(drawer.ShaderProperties.OfType<FloatPropertyViewModel>());

        void AssertState(string shaderId, string uniform, float expected)
        {
            Assert.Equal(shaderId, drawer.ShaderPicker.SelectedAssetId);
            Assert.Equal(shaderId, material.ShaderAssetId);
            Assert.Equal(expected, Convert.ToSingle(material.Properties[uniform]));
            Assert.Equal(expected, Number().Value);
            var color = Assert.Single(drawer.ShaderProperties.OfType<ColorPropertyViewModel>());
            Assert.Equal((0.25f, 0.5f, 0.75f, 1f), (color.R, color.G, color.B, color.A));
            Assert.Equal((false, 17), (material.UseDepth, material.SortingOrder));
        }
    }

    [AvaloniaFact]
    public async Task DepthAndConfirmedSortingOrderUseOneHistoryEntryEach()
    {
        var material = new Material("shader-id");
        material.Properties["strength"] = 1.5f;
        var runtime = new TestAssetRuntimeSyncService
        {
            RuntimeJson = """{"ShaderAssetId":"shader-id","UseDepth":true,"SortingOrder":25,"Properties":{"strength":1.5}}"""
        };
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.AddToLoadedAssets(new AssetInfo(new AssetMeta("material-id"), "C:/Assets/material.rmat"), material);
        using var history = new EditorCommandService();
        var command = new SetMaterialPropertyCommand(history, assets, runtime, new TestEngineRunner());
        using var drawer = new MaterialMonitorDrawerViewModel(new TestMaterialSelectable(), new TestAssetsService(material),
            null!, new TestShaderRegistry(), assets, new AssetTypeMapper(), runtime, null!, command);
        await WaitFor(() => drawer.SortingOrder == 25);
        Assert.False(history.CanUndo);
        Assert.Empty(runtime.Writes);
        runtime.RuntimeJson = "";

        var depth = new CheckBox { DataContext = drawer };
        var order = new ReiTextBox { DataContext = drawer };
        using var depthBinding = depth.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(drawer.UseDepth)) { Mode = BindingMode.TwoWay });
        using var orderBinding = order.Bind(ReiTextBox.TextProperty, new Binding(nameof(drawer.SortingOrder)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        depth.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        AssertState(false, 25);
        order.TextInternal = "42";
        AssertState(false, 25);
        Assert.Single(runtime.Writes);
        order.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(false, 42);

        await history.UndoAsync();
        AssertState(false, 25);
        await history.UndoAsync();
        AssertState(true, 25);
        Assert.False(history.CanUndo);
        await history.RedoAsync();
        AssertState(false, 25);
        await history.RedoAsync();
        AssertState(false, 42);
        Assert.False(history.CanRedo);
        Assert.Equal(6, runtime.Writes.Count);

        void AssertState(bool useDepth, int sortingOrder)
        {
            Assert.Equal((useDepth, sortingOrder), (drawer.UseDepth, drawer.SortingOrder));
            Assert.Equal((useDepth, sortingOrder), (material.UseDepth, material.SortingOrder));
            Assert.Equal(useDepth, depth.IsChecked);
            Assert.Equal(1.5f, Assert.IsType<FloatPropertyViewModel>(Assert.Single(drawer.ShaderProperties)).Value);
            Assert.Equal("shader-id", material.ShaderAssetId);
        }
    }

    [AvaloniaFact]
    public async Task ConfirmedNumberColorDragAndTextureEditsShareHistory()
    {
        var material = new Material("shader-id");
        material.SetSerializerVersion(1);
        material.Properties["strength"] = 1f;
        var runtime = new TestAssetRuntimeSyncService();
        var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
        assets.AddToLoadedAssets(new AssetInfo(new AssetMeta("material-id"), "C:/Assets/material.rmat"), material);
        assets.RegisterNewAssets([new AssetInfo(new AssetMeta("paint"), "C:/Assets/paint.png")]);
        var shaders = new TestShaderRegistry(
            new ShaderUniformInfo("tint", "vec4", ShaderUniformType.Color),
            new ShaderUniformInfo("texture", "sampler2D", ShaderUniformType.Texture));
        using var history = new EditorCommandService();
        var command = new SetMaterialPropertyCommand(history, assets, runtime, new TestEngineRunner());
        using var drawer = new MaterialMonitorDrawerViewModel(new TestMaterialSelectable(), new TestAssetsService(material),
            null!, shaders, assets, new AssetTypeMapper(), runtime, null!, command);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var number = Assert.Single(drawer.ShaderProperties.OfType<FloatPropertyViewModel>());
        var color = Assert.Single(drawer.ShaderProperties.OfType<ColorPropertyViewModel>());
        var texture = Assert.Single(drawer.ShaderProperties.OfType<AssetPropertyViewModel>()).AssetPicker!;
        var input = new ReiTextBox { DataContext = number, IsFloat = true };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(FloatPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        input.TextInternal = 2.5f.ToString(CultureInfo.CurrentCulture);
        AssertState(1, 1, "");
        Assert.False(history.CanUndo);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(2.5f, 1, "");

        color.BeginColorDrag();
        color.A = 0.75f;
        color.A = 0.5f;
        AssertState(2.5f, 0.5f, "");

        await WaitFor(() => runtime.Writes.Count == 2);
        var echoed = JObject.Parse(runtime.Writes[^1].Json);
        Assert.True(echoed.Remove(nameof(Asset.SerializerVersion)));
        runtime.RuntimeJson = echoed.ToString();
        var readsBeforeEcho = runtime.ReadCount;
        await WaitFor(() => runtime.ReadCount > readsBeforeEcho);
        Assert.Same(color, Assert.Single(drawer.ShaderProperties.OfType<ColorPropertyViewModel>()));
        runtime.RuntimeJson = "";
        color.EndColorDrag();
        Assert.True(texture.TryAssignAssetFromPath("C:/Assets/paint.png"));
        AssertState(2.5f, 0.5f, "paint");
        texture.ClearAsset();
        AssertState(2.5f, 0.5f, "");

        runtime.IsLoadedInRuntime = false;
        foreach (var (strength, alpha, id) in new[] { (2.5f, 0.5f, "paint"), (2.5f, 0.5f, ""), (2.5f, 1f, ""), (1f, 1f, "") })
        {
            await history.UndoAsync();
            AssertState(strength, alpha, id);
        }
        Assert.False(material.Properties.ContainsKey("texture"));
        Assert.False(material.Properties.ContainsKey("tint"));
        Assert.False(history.CanUndo);
        foreach (var (strength, alpha, id) in new[] { (2.5f, 1f, ""), (2.5f, 0.5f, ""), (2.5f, 0.5f, "paint"), (2.5f, 0.5f, "") })
        {
            await history.RedoAsync();
            AssertState(strength, alpha, id);
        }
        Assert.False(history.CanRedo);

        void AssertState(float strength, float alpha, string id)
        {
            Assert.Equal((strength, 0f, 0f, 0f, alpha, id), (number.Value, color.R, color.G, color.B, color.A, texture.SelectedAssetId));
            var properties = JObject.FromObject(material.Properties);
            Assert.Equal(strength, properties["strength"]!.Value<float>());
            Assert.Equal(alpha, properties["tint"]?["a"]?.Value<float>() ?? 1f);
            Assert.Equal(id, properties["texture"]?["Id"]?.Value<string>() ?? "");
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
