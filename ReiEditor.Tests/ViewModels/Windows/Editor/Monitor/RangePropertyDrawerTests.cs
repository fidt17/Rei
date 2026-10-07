using Avalonia.Headless.XUnit;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class RangePropertyDrawerTests
{
    private sealed class Selection : IAssetSelectable
    {
        public string AssetId => "config";
        public string AssetName => "Config";
        public string AssetPath => "Config.asset";
        public bool IsAssetSupportedInMonitor => true;
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class Assets(DataAsset asset) : IDataAssetService
    {
        public int SyncCount { get; private set; }
        public JObject? LastSynced { get; private set; }
        public Task<DataAsset?> Load(string id) => Task.FromResult<DataAsset?>(asset);
        public bool TrySyncRuntime(DataAsset value)
        {
            SyncCount++;
            LastSynced = SerializedPropertyJsonConverter.SerializeRuntimeProperties(value.Properties.Values);
            return true;
        }
        public Task<DataAsset?> Create(int id, string path) => throw new NotSupportedException();
        public bool TryGetDataAssetTypeId(string id, out int typeId) => throw new NotSupportedException();
        public bool TrySetProperty(DataAsset value, string name, object? data, out bool synced) => throw new NotSupportedException();
    }

    private sealed class Types(DataAssetTypeInfo type) : IDataAssetTypeRegistry
    {
        public DataAssetTypeInfo? GetDataAssetType(int id) => id == type.TypeId ? type : null;
        public DataAssetTypeInfo? GetDataAssetType(string name) => name == type.ObjectName ? type : null;
        public IEnumerable<DataAssetTypeInfo> GetDataAssetTypes() => [type];
        public int AllocateDataAssetTypeId() => throw new NotSupportedException();
        public Task RefreshAsync(IEnumerable<SerializableObjectInfo> declarations) => throw new NotSupportedException();
    }

    [AvaloniaFact]
    public async Task OldAssetUsesRootAndNestedRangeWithoutSerializationChangesAndFlushesPendingEdits()
    {
        var root = Schema("Config", "REI_HEADER(\"Lighting\") REI_RANGE(0, 8) SERIALIZE i32 Count = 4; SERIALIZE i32 Plain = 3; SERIALIZE Settings Options; HIDE_IN_EDITOR REI_RANGE(0, 8) SERIALIZE i32 Hidden = 4;");
        var nested = Schema("Settings", "REI_RANGE(0, 1, 0.25f) SERIALIZE f32 Weight = 0.5f;");
        var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        registry.Replace([root, nested], []);
        var types = new Types(new(1, root));
        var properties = new SerializedPropertiesService(registry, new TestLogger<SerializedPropertiesService>());
        var schemas = new DataAssetSchemaService(types, properties, new TestLogger<DataAssetSchemaService>());
        var asset = new DataAsset(1);
        Assert.True(schemas.TryRefresh(asset));
        asset.GetProperty("Count").Value = 12;
        var serializer = new ReiEditor.Models.Services.Serialization.JsonSerializer();
        var saved = serializer.Serialize(asset);
        Assert.DoesNotContain("Range", saved);
        var reloaded = serializer.Deserialize<DataAsset>(saved);
        Assert.True(schemas.TryRefresh(reloaded));
        var before = JObject.Parse(serializer.Serialize(reloaded));
        var assets = new Assets(reloaded);
        var drawer = Drawer(assets, registry, types);
        await drawer.LoadingTask;
        Assert.True(drawer.IsLoaded, drawer.StatusText);
        Assert.Equal(4, drawer.Properties.Count);
        Assert.IsType<PropertyHeaderViewModel>(drawer.Properties[0]);
        var count = Assert.IsType<RangePropertyViewModel>(drawer.Properties[1]);
        Assert.IsType<IntegerPropertyViewModel>(drawer.Properties[2]);
        var options = Assert.IsType<CustomPropertyViewModel>(drawer.Properties[3]);
        var weight = Assert.IsType<RangePropertyViewModel>(Assert.Single(options.Value));
        Assert.True(count.HasStatus);
        Assert.Equal(0, assets.SyncCount);
        Assert.True(JToken.DeepEquals(before, JObject.Parse(serializer.Serialize(reloaded))));
        count.SetSliderValue(2);
        weight.SetSliderValue(0.26);
        drawer.Dispose();
        Assert.Equal(1, assets.SyncCount);
        Assert.Equal(2, assets.LastSynced!["Count"]!["Value"]!.Value<int>());
        Assert.Equal(0.25f, assets.LastSynced["Options"]!["Value"]!["Weight"]!["Value"]!.Value<float>());
        var persisted = serializer.Serialize(reloaded);
        Assert.DoesNotContain("Range", persisted);
        var reopened = serializer.Deserialize<DataAsset>(persisted);
        Assert.True(schemas.TryRefresh(reopened));
        Assert.Equal(2, Convert.ToInt32(reopened.GetProperty("Count").Value));
        var children = Assert.IsType<Dictionary<string, SerializedProperty>>(reopened.GetProperty("Options").Value);
        Assert.Equal(0.25f, Convert.ToSingle(children["Weight"].Value));
        count.SetSliderValue(6);
        Assert.Equal(2, reloaded.GetProperty("Count").Value);
    }

    [AvaloniaFact]
    public async Task SourceRangeChangesAndRemovalTakeEffectOnDrawerRefresh()
    {
        var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        var asset = new DataAsset(1);
        var properties = new SerializedPropertiesService(registry, new TestLogger<SerializedPropertiesService>());
        foreach (var (source, expectedMaximum) in new (string, double?)[]
        {
            ("REI_RANGE(0, 8) SERIALIZE i32 Count = 4;", 8),
            ("REI_RANGE(0, 4) SERIALIZE i32 Count = 4;", 4),
            ("SERIALIZE i32 Count = 4;", null)
        })
        {
            var root = Schema("Config", source);
            registry.Replace([root], []);
            var types = new Types(new(1, root));
            Assert.True(new DataAssetSchemaService(types, properties, new TestLogger<DataAssetSchemaService>()).TryRefresh(asset));
            using var drawer = Drawer(new Assets(asset), registry, types);
            await drawer.LoadingTask;
            if (expectedMaximum is { } maximum)
                Assert.Equal(maximum, Assert.IsType<RangePropertyViewModel>(Assert.Single(drawer.Properties)).Range.Maximum);
            else
                Assert.IsType<IntegerPropertyViewModel>(Assert.Single(drawer.Properties));
            Assert.Equal(4, Convert.ToInt32(asset.GetProperty("Count").Value));
        }
    }

    private static SerializableObjectInfo Schema(string name, string source) => new("game", name, false, new ObjectFile<string>(source, name + ".h"), new SourceFilesUtility(null!, null!, null!).GetSerializedProperties(source), name + ".h");
    private static DataAssetMonitorDrawerViewModel Drawer(Assets assets, ISerializableObjectsRegistry registry, IDataAssetTypeRegistry types) => new(new Selection(), assets, registry, types, null!, null!, null!, null!, null!, null!, null!);
}