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

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies bounded material drawer loading and disposal persistence without timer-dependent assertions.
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
    /// Supplies empty shader registry for no-shader material branch.
    /// </summary>
    private sealed class TestShaderRegistry : IShaderRegistry
    {
        public IReadOnlyDictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();
        public bool TryGetById(string assetId, [NotNullWhen(true)] out Shader? shader)
        {
            shader = null;
            return false;
        }

        public Task RefreshShaders() => throw new NotSupportedException();
    }

    /// <summary>
    /// Records immediate runtime writes performed during disposal.
    /// </summary>
    private sealed class TestAssetRuntimeSyncService : IAssetRuntimeSyncService
    {
        public List<(string Id, string Json)> Writes { get; } = [];
        public bool TryGetAssetData(string assetId, out string jsonData)
        {
            jsonData = "";
            return false;
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
}
