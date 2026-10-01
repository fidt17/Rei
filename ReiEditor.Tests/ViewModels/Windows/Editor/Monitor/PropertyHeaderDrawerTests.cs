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
public sealed class PropertyHeaderDrawerTests
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
        public Task<DataAsset?> Load(string id) => Task.FromResult<DataAsset?>(asset);
        public bool TrySyncRuntime(DataAsset value) { SyncCount++; return true; }
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
    public async Task RootAndNestedDrawersUseSourceHeadersAfterReloadWithoutWritingValues()
    {
        var parser = new SourceFilesUtility(null!, null!, null!);
        SerializableObjectInfo Schema(string name, string source) => new("game", name, false, new ObjectFile<string>(source, name + ".h"), parser.GetSerializedProperties(source), name + ".h");
        var nested = Schema("Settings", "REI_HEADER(\"Nested\") SERIALIZE float Zulu = 3; SERIALIZE float Alpha = 1; REI_HEADER(\"Hidden Nested\") HIDE_IN_EDITOR SERIALIZE int Internal = 7;");
        var root = Schema("Config", "REI_HEADER(\"Root\") SERIALIZE Settings Options; REI_HEADER(\"Hidden Root\") HIDE_IN_EDITOR SERIALIZE int Internal = 1;");
        var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        registry.Replace([root, nested], []);
        var types = new Types(new(1, root));
        var properties = new SerializedPropertiesService(registry, new TestLogger<SerializedPropertiesService>());
        var schemaService = new DataAssetSchemaService(types, properties, new TestLogger<DataAssetSchemaService>());
        var initial = new DataAsset(1);
        Assert.True(schemaService.TryRefresh(initial));
        var options = (Dictionary<string, SerializedProperty>)initial.GetProperty("Options").Value!;
        initial.GetProperty("Options").Value = new Dictionary<string, SerializedProperty> { ["Alpha"] = options["Alpha"], ["Internal"] = options["Internal"], ["Zulu"] = options["Zulu"] };
        var serializer = new ReiEditor.Models.Services.Serialization.JsonSerializer();
        var reloaded = serializer.Deserialize<DataAsset>(serializer.Serialize(initial));
        Assert.True(schemaService.TryRefresh(reloaded));
        var before = JObject.Parse(serializer.Serialize(reloaded));
        var assets = new Assets(reloaded);
        var drawer = new DataAssetMonitorDrawerViewModel(new Selection(), assets, registry, types, null!, null!, null!, null!, null!, null!, null!);
        await drawer.LoadingTask;

        Assert.True(drawer.IsLoaded, drawer.StatusText);
        Assert.Collection(drawer.Properties,
            row => Assert.Equal("Root", Assert.IsType<PropertyHeaderViewModel>(row).Text),
            row => Assert.IsType<CustomPropertyViewModel>(row));
        var child = Assert.IsType<CustomPropertyViewModel>(drawer.Properties[1]);
        Assert.Collection(child.Value,
            row => Assert.Equal("Nested", Assert.IsType<PropertyHeaderViewModel>(row).Text),
            row => Assert.Equal("Zulu", Assert.IsType<FloatPropertyViewModel>(row).PropertyName.Value),
            row => Assert.Equal("Alpha", Assert.IsType<FloatPropertyViewModel>(row).PropertyName.Value));
        child.SwitchExpandState();
        child.SwitchExpandState();
        drawer.Dispose();
        Assert.True(JToken.DeepEquals(before, JObject.Parse(serializer.Serialize(reloaded))));
        Assert.Equal(0, assets.SyncCount);
    }
}
