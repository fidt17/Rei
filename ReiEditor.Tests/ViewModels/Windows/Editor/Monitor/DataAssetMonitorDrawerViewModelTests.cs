using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class DataAssetMonitorDrawerViewModelTests
{
    private sealed class TestSelection : IAssetSelectable
    {
        public string AssetId => "config";
        public string AssetName => "Config";
        public string AssetPath => "Config.asset";
        public bool IsAssetSupportedInMonitor => true;
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class TestDataAssets(Task<DataAsset?> load) : IDataAssetService
    {
        public int SyncCount { get; private set; }
        public Task<DataAsset?> Load(string assetId) => load;
        public bool TrySyncRuntime(DataAsset asset) { SyncCount++; return true; }
        public Task<DataAsset?> Create(int dataAssetTypeId, string projectPath) => throw new NotSupportedException();
        public bool TryGetDataAssetTypeId(string assetId, out int dataAssetTypeId) => throw new NotSupportedException();
        public bool TrySetProperty(DataAsset asset, string propertyName, object? value, out bool runtimeSynced) => throw new NotSupportedException();
    }

    private sealed class TestTypes : IDataAssetTypeRegistry
    {
        private readonly DataAssetTypeInfo _type = new(1, new SerializableObjectInfo(
            "game", "Config", false, new ObjectFile<string>("", "Config.h"), new(), "Config.h"));
        public DataAssetTypeInfo? GetDataAssetType(int id) => id == 1 ? _type : null;
        public DataAssetTypeInfo? GetDataAssetType(string name) => name == "Config" ? _type : null;
        public IEnumerable<DataAssetTypeInfo> GetDataAssetTypes() => [_type];
        public int AllocateDataAssetTypeId() => throw new NotSupportedException();
        public Task RefreshAsync(IEnumerable<SerializableObjectInfo> declarations) => throw new NotSupportedException();
    }

    [AvaloniaFact]
    public async Task LoadsPropertiesAndDoesNotWriteRuntimeOnReadOnlyDispose()
    {
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(CreateAsset()));
        var drawer = CreateDrawer(assets);
        await drawer.LoadingTask;

        Assert.True(drawer.IsLoaded);
        Assert.Equal("", drawer.StatusText);
        Assert.Equal("Type: Config (1)", drawer.TypeLabel);
        Assert.Single(drawer.Properties);

        drawer.Dispose();
        drawer.Dispose();
        Assert.Equal(0, assets.SyncCount);
    }

    [AvaloniaFact]
    public async Task DisposedDrawerDoesNotPublishLateLoadOrSubscribeToProperties()
    {
        var completion = new TaskCompletionSource<DataAsset?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var assets = new TestDataAssets(completion.Task);
        var drawer = CreateDrawer(assets);
        drawer.Dispose();
        var asset = CreateAsset();

        completion.SetResult(asset);
        await drawer.LoadingTask;
        asset.GetProperty("value").Value = 9f;

        Assert.False(drawer.IsLoaded);
        Assert.Empty(drawer.Properties);
        Assert.Equal(0, assets.SyncCount);
    }

    [AvaloniaFact]
    public async Task FailedLoadReplacesLoadingStatus()
    {
        using var drawer = CreateDrawer(new TestDataAssets(Task.FromException<DataAsset?>(new IOException("read failed"))));
        await drawer.LoadingTask;

        Assert.False(drawer.IsLoaded);
        Assert.Contains("read failed", drawer.StatusText);
        Assert.DoesNotContain("Loading", drawer.StatusText);
    }

    private static DataAsset CreateAsset()
    {
        var asset = new DataAsset(1);
        asset.AddProperty(new SerializedProperty("value", SerializedTypeEnum.Float, 1f, "f32", null));
        return asset;
    }

    private static DataAssetMonitorDrawerViewModel CreateDrawer(IDataAssetService assets)
        => new(new TestSelection(), assets, null!, new TestTypes(), null!, null!, null!, null!, null!, null!, null!);
}
