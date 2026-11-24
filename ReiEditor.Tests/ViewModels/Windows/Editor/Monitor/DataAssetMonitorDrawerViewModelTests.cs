using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Commands;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Collection;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;
using ReiEditor.Views.Controls.TextBoxes;

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
        private readonly SerializedPropertiesService _properties = new(null!, new TestLogger<SerializedPropertiesService>());

        public int SyncCount { get; private set; }
        public Task<DataAsset?> Load(string assetId) => load;
        public bool TrySyncRuntime(DataAsset asset) { SyncCount++; return true; }
        public Task<DataAsset?> Create(int dataAssetTypeId, string projectPath) => throw new NotSupportedException();
        public bool TryGetDataAssetTypeId(string assetId, out int dataAssetTypeId) => throw new NotSupportedException();
        public bool TrySetProperty(DataAsset asset, string propertyName, object? value, out bool runtimeSynced)
        {
            _properties.ApplyValue(asset.GetProperty(propertyName), value);
            runtimeSynced = TrySyncRuntime(asset);
            return true;
        }
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

    [AvaloniaFact]
    public async Task CommittedNumericEditUpdatesTheDrawerAndSyncsOncePerHistoryStep()
    {
        var asset = CreateAsset();
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        var command = new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner());
        using var drawer = CreateDrawer(assets, command);
        await drawer.LoadingTask;
        var number = Assert.IsType<FloatPropertyViewModel>(Assert.Single(drawer.Properties));
        var input = new ReiTextBox { DataContext = number };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(FloatPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        input.TextInternal = 3.5f.ToString(CultureInfo.CurrentCulture);
        Assert.False(history.CanUndo);
        Assert.Equal(0, assets.SyncCount);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3.5f, number.Value);
        Assert.Equal(1, assets.SyncCount);

        await history.UndoAsync();
        Assert.Equal(1f, number.Value);
        Assert.Equal(2, assets.SyncCount);
        Assert.False(history.CanUndo);

        await history.RedoAsync();
        Assert.Equal(3.5f, number.Value);
        drawer.Dispose();
        Assert.Equal(3, assets.SyncCount);
        Assert.False(history.CanRedo);
    }

    [AvaloniaFact]
    public async Task CheckboxEnumAndTextReplayInOrderInTheDrawer()
    {
        var asset = new DataAsset(1);
        asset.AddProperty(new SerializedProperty("enabled", SerializedTypeEnum.Boolean, true, "bool", null));
        asset.AddProperty(new SerializedProperty("mode", SerializedTypeEnum.Enum, 1L, "Mode", null));
        asset.AddProperty(new SerializedProperty("label", SerializedTypeEnum.String, "Before", "string", null));
        var enums = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        enums.Replace([], [new SerializableEnum { EnumName = "Mode", Options = new() { ["First"] = 1, ["Second"] = 2 } }]);
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        var command = new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner());
        using var drawer = CreateDrawer(assets, command, enums);
        await drawer.LoadingTask;
        var enabled = Assert.Single(drawer.Properties.OfType<BooleanPropertyViewModel>());
        var mode = Assert.Single(drawer.Properties.OfType<EnumPropertyViewModel>());
        var label = Assert.Single(drawer.Properties.OfType<StringPropertyViewModel>());
        var input = new ReiTextBox { DataContext = label };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(StringPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        var states = new[] { (true, "First", "Before"), (false, "First", "Before"), (false, "Second", "Before"), (false, "Second", "After") };

        Assert.Equal(states[0], Read());
        enabled.Value = false;
        Assert.Equal(states[1], Read());
        mode.SelectedValue = "Second";
        Assert.Equal(states[2], Read());
        input.TextInternal = "After";
        Assert.Equal(states[2], Read());
        Assert.Equal(2, assets.SyncCount);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(states[3], Read());

        for (var i = 2; i >= 0; i--)
        {
            await history.UndoAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(states[i], Read());
        }
        Assert.False(history.CanUndo);
        for (var i = 1; i < states.Length; i++)
        {
            await history.RedoAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(states[i], Read());
        }
        Assert.False(history.CanRedo);
        drawer.Dispose();
        Assert.Equal(9, assets.SyncCount);

        (bool, string, string) Read() => (enabled.Value, mode.SelectedValue, label.Value);
    }

    [AvaloniaFact]
    public async Task CommittedVectorCoordinatesReplayWithoutRewritingTheOtherAxes()
    {
        var asset = new DataAsset(1);
        var vector2Fields = AddVector("position2D", "Vector2", ("x", 1.25), ("y", 0.1));
        var vector3Fields = AddVector("position3D", "Vector3", ("x", -0.27), ("y", 2.125), ("z", -3.5));
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        using var drawer = CreateDrawer(assets, new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner()));
        await drawer.LoadingTask;
        var vector2 = Assert.Single(drawer.Properties.OfType<Vector2PropertyViewModel>());
        var vector3 = Assert.Single(drawer.Properties.OfType<Vector3PropertyViewModel>());
        var xInput = new ReiTextBox { DataContext = vector2, IsFloat = true };
        var zInput = new ReiTextBox { DataContext = vector3, IsFloat = true };
        using var xBinding = xInput.Bind(ReiTextBox.TextProperty, new Binding(nameof(Vector2PropertyViewModel.X)) { Mode = BindingMode.TwoWay });
        using var zBinding = zInput.Bind(ReiTextBox.TextProperty, new Binding(nameof(Vector3PropertyViewModel.Z)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        xInput.TextInternal = 5.75f.ToString(CultureInfo.CurrentCulture);
        AssertState(1.25f, -3.5f);
        Assert.False(history.CanUndo);
        xInput.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(5.75f, -3.5f);
        zInput.TextInternal = (-10.5f).ToString(CultureInfo.CurrentCulture);
        zInput.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(5.75f, -10.5f);

        await history.UndoAsync();
        AssertState(5.75f, -3.5f);
        await history.UndoAsync();
        AssertState(1.25f, -3.5f);
        Assert.False(history.CanUndo);
        await history.RedoAsync();
        AssertState(5.75f, -3.5f);
        await history.RedoAsync();
        AssertState(5.75f, -10.5f);
        Assert.False(history.CanRedo);
        drawer.Dispose();
        Assert.Equal(6, assets.SyncCount);

        void AssertState(float x, float z)
        {
            Assert.Equal((x, 0.1f), (vector2.X, vector2.Y));
            Assert.Equal((-0.27f, 2.125f, z), (vector3.X, vector3.Y, vector3.Z));
            Assert.Equal(0.1, Assert.IsType<double>(vector2Fields["y"].Value));
            Assert.Equal(-0.27, Assert.IsType<double>(vector3Fields["x"].Value));
            Assert.Equal(2.125, Assert.IsType<double>(vector3Fields["y"].Value));
        }

        Dictionary<string, SerializedProperty> AddVector(string name, string sourceType, params (string Axis, double Value)[] coordinates)
        {
            var property = new SerializedProperty(name, SerializedTypeEnum.Custom, null, sourceType, null);
            var fields = coordinates.ToDictionary(coordinate => coordinate.Axis,
                coordinate => new SerializedProperty(coordinate.Axis, SerializedTypeEnum.Float, coordinate.Value, "f32", property));
            property.Value = fields;
            asset.AddProperty(property);
            return fields;
        }
    }

    [AvaloniaFact]
    public async Task ColorDragsAndConfirmedChannelEditReplayInOrder()
    {
        var asset = new DataAsset(1);
        var property = new SerializedProperty("tint", SerializedTypeEnum.Custom, null, "Color", null);
        var original = new SerializedColorValue(0.1f, 0.5f, 0.75f, 0.6f);
        var channels = new Dictionary<string, double> { ["r"] = 0.1, ["g"] = 0.5, ["b"] = 0.75, ["a"] = 0.6 }
            .ToDictionary(field => field.Key, field => new SerializedProperty(field.Key, SerializedTypeEnum.Float, field.Value, "f32", property));
        property.Value = channels;
        asset.AddProperty(property);
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        using var drawer = CreateDrawer(assets, new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner()));
        await drawer.LoadingTask;
        var color = Assert.Single(drawer.Properties.OfType<ColorPropertyViewModel>());

        color.BeginColorDrag();
        color.A = 0.5f;
        color.A = 0.25f;
        var afterAlpha = original with { A = 0.25f };
        AssertColor(afterAlpha);
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (assets.SyncCount == 0 && wait.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
        Assert.Equal(1, assets.SyncCount);
        Assert.False(history.CanUndo);
        color.EndColorDrag();
        Assert.Equal(0.1, Assert.IsType<double>(channels["r"].Value));

        color.BeginColorDrag();
        color.H = 20;
        color.S = 0.5f;
        color.EndColorDrag();
        var afterWheel = new SerializedColorValue(color.R, color.G, color.B, afterAlpha.A);
        Assert.NotEqual(afterAlpha, afterWheel);
        AssertColor(afterWheel);

        var input = new ReiTextBox { DataContext = color, IsFloat = true };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(ColorPropertyViewModel.R)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();
        input.TextInternal = 0.25f.ToString(CultureInfo.CurrentCulture);
        AssertColor(afterWheel);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        var afterInput = afterWheel with { R = 0.25f };
        AssertColor(afterInput);

        foreach (var expected in new[] { afterWheel, afterAlpha, original })
        {
            await history.UndoAsync();
            AssertColor(expected);
        }
        Assert.False(history.CanUndo);
        foreach (var expected in new[] { afterAlpha, afterWheel, afterInput })
        {
            await history.RedoAsync();
            AssertColor(expected);
        }
        Assert.False(history.CanRedo);

        color.A = 0.75f;
        var afterAlphaStep = afterInput with { A = 0.75f };
        AssertColor(afterAlphaStep);
        await history.UndoAsync();
        AssertColor(afterInput);
        await history.RedoAsync();
        AssertColor(afterAlphaStep);

        void AssertColor(SerializedColorValue expected)
        {
            Assert.Equal(expected, SerializedColorValue.Read(property.Value));
            Assert.Equal(expected, new SerializedColorValue(color.R, color.G, color.B, color.A));
        }
    }

    [AvaloniaFact]
    public async Task AssetPickerSelectionAndClearReplayInOrder()
    {
        var asset = new DataAsset(1);
        var reference = new SerializedProperty("dependency", SerializedTypeEnum.Custom, null, "AssetRef<Config>", null, templateTypeName: "Config");
        var id = new SerializedProperty("Id", SerializedTypeEnum.String, "first", "string", reference);
        reference.Value = new Dictionary<string, SerializedProperty> { ["Id"] = id };
        asset.AddProperty(reference);
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([
            new DataAssetInfo(new AssetMeta("first"), "First.asset", 1),
            new DataAssetInfo(new AssetMeta("second"), "Second.asset", 1)
        ]);
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        using var drawer = CreateDrawer(assets, new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner()), assetRegistry: registry);
        await drawer.LoadingTask;
        var picker = Assert.Single(drawer.Properties.OfType<AssetPropertyViewModel>()).AssetPicker!;
        AssertSelection("first");
        Assert.False(history.CanUndo);

        picker.RefreshSearchResultsForAll();
        var second = Assert.Single(picker.SearchResults, item => item.AssetId == "second");
        second.SelectCommand.Execute(null);
        AssertSelection("second");
        picker.ClearAsset();
        AssertSelection("");

        await history.UndoAsync();
        AssertSelection("second");
        await history.UndoAsync();
        AssertSelection("first");
        Assert.False(history.CanUndo);
        await history.RedoAsync();
        AssertSelection("second");
        await history.RedoAsync();
        AssertSelection("");
        Assert.False(history.CanRedo);
        drawer.Dispose();
        Assert.Equal(6, assets.SyncCount);

        void AssertSelection(string expected)
        {
            Assert.Equal(expected, id.Value);
            Assert.Equal(expected, picker.SelectedAssetId);
            Assert.False(picker.IsMissingAsset);
        }
    }

    [AvaloniaFact]
    public async Task ConfirmedNestedEditsReplayOnlyTheirOwnField()
    {
        var asset = new DataAsset(1);
        var settings = new SerializedProperty("settings", SerializedTypeEnum.Custom, null, "Settings", null);
        settings.Value = new Dictionary<string, SerializedProperty>
        {
            ["value"] = new("value", SerializedTypeEnum.Float, 1.25, "f32", settings),
            ["label"] = new("label", SerializedTypeEnum.String, "Before", "string", settings)
        };
        asset.AddProperty(settings);
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        using var drawer = CreateDrawer(assets, new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner()));
        await drawer.LoadingTask;
        var group = Assert.Single(drawer.Properties.OfType<CustomPropertyViewModel>());
        var number = Assert.Single(group.Value.OfType<FloatPropertyViewModel>());
        var label = Assert.Single(group.Value.OfType<StringPropertyViewModel>());
        var numberInput = new ReiTextBox { DataContext = number, IsFloat = true };
        var labelInput = new ReiTextBox { DataContext = label };
        using var numberBinding = numberInput.Bind(ReiTextBox.TextProperty, new Binding(nameof(FloatPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        using var labelBinding = labelInput.Bind(ReiTextBox.TextProperty, new Binding(nameof(StringPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        numberInput.TextInternal = 3.5f.ToString(CultureInfo.CurrentCulture);
        AssertState(1.25f, "Before");
        Assert.False(history.CanUndo);
        numberInput.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(3.5f, "Before");
        labelInput.TextInternal = "After";
        AssertState(3.5f, "Before");
        labelInput.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertState(3.5f, "After");

        await history.UndoAsync();
        AssertState(3.5f, "Before");
        await history.UndoAsync();
        AssertState(1.25f, "Before");
        Assert.False(history.CanUndo);
        await history.RedoAsync();
        AssertState(3.5f, "Before");
        await history.RedoAsync();
        AssertState(3.5f, "After");
        Assert.False(history.CanRedo);
        drawer.Dispose();
        Assert.Equal(6, assets.SyncCount);

        void AssertState(float value, string text) => Assert.Equal((value, text), (number.Value, label.Value));
    }

    [AvaloniaFact]
    public async Task CollectionEditAddAndRemoveReplayInOrder()
    {
        var asset = new DataAsset(1);
        var property = new SerializedProperty("weights", SerializedTypeEnum.Collection, null, "vector<f32>", null,
            "f32", SerializedTypeEnum.Float, "f32");
        var original = new[] { 0.25f, 0.5f, 1f };
        property.Value = original.Select((value, index) =>
            new SerializedProperty($"[{index}]", SerializedTypeEnum.Float, value, "f32", property)).ToList();
        asset.AddProperty(property);
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.AddToLoadedAssets(new AssetInfo(new AssetMeta("config"), "Config.asset"), asset);
        using var history = new EditorCommandService();
        using var drawer = CreateDrawer(assets, new SetDataAssetPropertyCommand(history, assets, registry, new TestEngineRunner()));
        await drawer.LoadingTask;
        var list = Assert.Single(drawer.Properties.OfType<CollectionPropertyViewModel>());
        var number = Assert.IsType<FloatPropertyViewModel>(Assert.Single(list.Value[1].Value));
        var input = new ReiTextBox { DataContext = number, IsFloat = true };
        using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(FloatPropertyViewModel.Value)) { Mode = BindingMode.TwoWay });
        Dispatcher.UIThread.RunJobs();

        float[] edited = [0.25f, 2.5f, 1f];
        float[] added = [0.25f, 2.5f, 1f, 0f];
        float[] removed = [0.25f, 1f, 0f];
        input.TextInternal = 2.5f.ToString(CultureInfo.CurrentCulture);
        AssertList(original);
        Assert.False(history.CanUndo);
        input.LoseFocus();
        Dispatcher.UIThread.RunJobs();
        AssertList(edited);
        list.AddItemCommand.Execute(null);
        AssertList(added);
        list.Value[1].RemoveCommand.Execute(null);
        AssertList(removed);

        foreach (var expected in new[] { added, edited, original })
        {
            await history.UndoAsync();
            AssertList(expected);
        }
        Assert.False(history.CanUndo);
        foreach (var expected in new[] { edited, added, removed })
        {
            await history.RedoAsync();
            AssertList(expected);
        }
        Assert.False(history.CanRedo);
        drawer.Dispose();
        Assert.Equal(9, assets.SyncCount);

        void AssertList(float[] expected)
        {
            Assert.Equal(expected, list.Value.Select(item => Assert.IsType<FloatPropertyViewModel>(Assert.Single(item.Value)).Value));
            var items = Assert.IsType<List<SerializedProperty>>(property.Value);
            Assert.Equal(expected, items.Select(item => Convert.ToSingle(item.Value)));
        }
    }

    [AvaloniaFact]
    public async Task RapidEditsAreDebouncedAndPendingEditFlushesOnceOnDispose()
    {
        var asset = CreateAsset();
        var assets = new TestDataAssets(Task.FromResult<DataAsset?>(asset));
        var drawer = CreateDrawer(assets);
        await drawer.LoadingTask;
        asset.GetProperty("value").Value = 2f;
        asset.GetProperty("value").Value = 3f;
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (assets.SyncCount == 0 && wait.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
        Assert.Equal(1, assets.SyncCount);

        asset.GetProperty("value").Value = 4f;
        drawer.Dispose();
        Assert.Equal(2, assets.SyncCount);
        await Task.Delay(100);
        asset.GetProperty("value").Value = 5f;
        Assert.Equal(2, assets.SyncCount);
    }

    private static DataAsset CreateAsset()
    {
        var asset = new DataAsset(1);
        asset.AddProperty(new SerializedProperty("value", SerializedTypeEnum.Float, 1f, "f32", null));
        return asset;
    }

    private static DataAssetMonitorDrawerViewModel CreateDrawer(IDataAssetService assets, SetDataAssetPropertyCommand? command = null,
        ISerializableObjectsRegistry? serializableObjects = null, IAssetRegistry? assetRegistry = null)
        => new(new TestSelection(), assets, serializableObjects!, new TestTypes(), null!, assetRegistry!, new AssetTypeMapper(), null!, null!, null!, null!, command);
}
