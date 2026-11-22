using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Component;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.RectTransform;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Collection;
using ReiEditor.Models.Services.Render;
using ReiEditor.Views.Controls.ColorPicker;
using ReiEditor.Views.Controls.Assets;
using Avalonia.Data;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;
using ReiEditor.Views.Controls.TextBoxes;
using Avalonia.Headless;
using Avalonia.Threading;
using ReiEditor.Models.Services.Commands;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ReiEditor.Tests.Models.Services.Engine;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "EngineActionCallbacks")]
public sealed class EngineApiActionCallbackTests
{
    private static string? NativeSkipReason => !OperatingSystem.IsWindows() ? "Native callbacks require Windows x64."
        : Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1"
            ? "Set REI_RUN_ENGINE_TESTS=1 and REI_TEST_NATIVE_PROJECT_DLL to the built native project fixture." : null;

    private sealed class NativeCallbackFactAttribute : FactAttribute
    {
        public NativeCallbackFactAttribute() { Skip = NativeSkipReason; }
    }

    private sealed class NativeCallbackTheoryAttribute : TheoryAttribute
    {
        public NativeCallbackTheoryAttribute() { Skip = NativeSkipReason; }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EmitActionDelegate(ulong id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateActionEngineDelegate(IntPtr resources);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StartActionEngineDelegate(IntPtr createEngine);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong RecordTransformActionDelegate(int mode, int targetCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReadTransformDelegate(int index, [Out] float[] values);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReplayActionDelegate(ulong id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReadEntityWorldTransformDelegate(int sceneId, [Out] float[] values);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EntityIdDelegate(int sceneId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PrepareRectMoveDelegate(int kind, int targetCount, [Out] int[] ids);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MoveRectPointerDelegate(int dx, int dy);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReadRectMoveDelegate(int index, [Out] float[] values);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PrepareRectCanvasesDelegate(float sourceScale, float destinationScale, [Out] int[] canvasIds);

    private sealed class InspectorComponentRegistry(params BehaviourAssetInfo[] behaviours) : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours { get; } = behaviours.ToDictionary(behaviour => behaviour.BehaviourId);
        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour) => Behaviours.TryGetValue(id, out behaviour);
        public int? GetIdByName(string name) => behaviours.FirstOrDefault(behaviour => behaviour.ObjectName == name)?.BehaviourId;
        public int AllocateBehaviourId() => throw new NotSupportedException();
        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    private sealed class InspectorDataAssetTypeRegistry(DataAssetTypeInfo type) : IDataAssetTypeRegistry
    {
        public IEnumerable<DataAssetTypeInfo> GetDataAssetTypes() => [type];
        public DataAssetTypeInfo? GetDataAssetType(int id) => id == type.TypeId ? type : null;
        public DataAssetTypeInfo? GetDataAssetType(string name) => name == type.ObjectName ? type : null;
        public int AllocateDataAssetTypeId() => throw new NotSupportedException();
        public Task RefreshAsync(IEnumerable<SerializableObjectInfo> declarations) => throw new NotSupportedException();
    }

    private sealed class InspectorFactory<T>(Func<object[], T> create) : IFactory<T> where T : class
    {
        public T CreateInstance() => create([]);
        public T CreateInstance(params object[] parameters) => create(parameters);
    }

    [NativeCallbackFact]
    public Task InspectorAddComponentRecordsOneUndo() => VerifyInspectorAddComponentAsync(editNumber: false);

    [NativeCallbackFact]
    public Task InspectorAddComponentThenEditNumberUndoAndRedoInOrder() => VerifyInspectorAddComponentAsync(editNumber: true);

    private static Task VerifyInspectorAddComponentAsync(bool editNumber)
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
            var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
            Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
            var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
            using var session = factory.CreateIsolatedSession(path!);
            var api = session.EngineApi;
            CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
            try
            {
                api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
                api.MarkEngineStarted();
                const int PROBE_ID = 7102;
                const int TRANSFORM_ID = 7199;
                var entityApi = new EntityApi(api);
                var id = entityApi.CreateNewEntity("Inspector component history")!.EntityId!.Value;
                var entity = new GameEntity(id, "Inspector component history");
                var scenes = new TestSceneManagementService(entity);
                var registry = new InspectorComponentRegistry(
                    new BehaviourAssetInfo("", "Transform", TRANSFORM_ID, new ObjectFile<string>("", ""), new()
                    {
                        ["_scale"] = new(SerializedTypeEnum.Custom, "Vector3", null, SerializedTypeEnum.Invalid, null, null, null, false)
                    }, [], ""),
                    new BehaviourAssetInfo("", "ProbeB", PROBE_ID, new ObjectFile<string>("", ""), new()
                    {
                        ["Number"] = new(SerializedTypeEnum.Integer, "i32", null, SerializedTypeEnum.Invalid, null, null, "97", false)
                    }, [], ""));
                var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
                var coordinate = new SerializableObjectInfo.SerializedPropertyData(SerializedTypeEnum.Float, "f32",
                    null, SerializedTypeEnum.Invalid, null, null, "1", false);
                objects.Replace([new SerializableObjectInfo("rei::math", "Vector3", false, new ObjectFile<string>("", ""),
                    new() { ["x"] = coordinate, ["y"] = coordinate, ["z"] = coordinate }, "")], []);
                var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
                var components = new BehaviourComponentsService(new TestLogger<BehaviourComponentsService>(), registry, properties);
                var runner = new TestEngineRunner { ActiveMode = EngineRunMode.EditorMode };
                runner.Active.Value = runner.EditorActive.Value = true;
                var syncLogger = new TestLogger<EntitySyncService>();
                var applier = new EntityStateApplier(syncLogger, registry, components, properties);
                var importer = new TestAssetImporter();
                using var sync = new EntitySyncService(importer, entityApi, runner, components,
                    new BehaviourSyncService(importer, runner, entityApi, syncLogger, applier), applier);
                var management = new EntityManagementService(new TestLogger<EntityManagementService>(), scenes, components,
                    entityApi, registry, runner, sync, null!);
                sync.UpdateEntityState(entity);
                using var commands = new EditorCommandService();
                var add = new AddComponentCommand(commands, scenes, management, registry, components, runner);
                var setProperty = new SetComponentPropertyCommand(commands, scenes, runner);
                var custom = new RectTransformCustomPropertiesProvider(registry, objects, null!, null!, null!, null!, scenes, null!, runner, entityApi, null!);
                using var inspector = new EntityMonitorDrawerViewModel(entity,
                    new InspectorFactory<EntityInfoComponentDrawerViewModel>(args => new((GameEntity)args[0], null!, null!)),
                    new InspectorFactory<BehaviourComponentDrawerViewModel>(args => new((GameEntity)args[0], (BehaviourComponent)args[1],
                        registry, management, components, objects, null!, null!, null!, null!, null!, scenes, null!, null!, custom, setProperty)),
                    registry, add);
                var theme = new FluentTheme();
                var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
                Application.Current!.Styles.Add(theme);
                Application.Current.Styles.Add(styles);
                var view = new EntityMonitorDrawer { DataContext = inspector };
                var window = new Window { Content = view, Width = 400, Height = 480 };
                try
                {
                    window.Show();
                    AssertComponentNumber(null);
                    Assert.False(commands.CanUndo);
                    Assert.Single(inspector.BehaviourSelection, item => item.BehaviourId == PROBE_ID).SelectCommand.Execute(null);
                    AssertComponentNumber(97);
                    Assert.True(commands.CanUndo);

                    if (editNumber)
                    {
                        var input = FindProbeDrawer()!.GetVisualDescendants().OfType<ReiTextBox>().Single();
                        input.TextInternal = "42";
                        input.LoseFocus();
                        AssertComponentNumber(42);

                        await commands.UndoAsync();
                        AssertComponentNumber(97);
                        await commands.UndoAsync();
                        AssertComponentNumber(null);
                        Assert.False(commands.CanUndo);

                        await commands.RedoAsync();
                        AssertComponentNumber(97);
                        await commands.RedoAsync();
                        AssertComponentNumber(42);
                        Assert.False(commands.CanRedo);
                        Assert.Empty(syncLogger.Entries);
                        return true;
                    }

                    await commands.UndoAsync();
                    AssertComponentNumber(null);
                    Assert.False(commands.CanUndo);
                    Assert.True(commands.CanRedo);

                    await commands.RedoAsync();
                    AssertComponentNumber(97);
                    Assert.False(commands.CanRedo);
                    Assert.Empty(syncLogger.Entries);
                }
                finally
                {
                    window.Close();
                    Application.Current.Styles.Remove(styles);
                    Application.Current.Styles.Remove(theme);
                }

                BehaviourComponentDrawer? FindProbeDrawer() => view.GetVisualDescendants().OfType<BehaviourComponentDrawer>()
                    .SingleOrDefault(drawer => drawer.DataContext is BehaviourComponentDrawerViewModel vm && vm.BehaviourComponent.Id == PROBE_ID);

                void AssertComponentNumber(int? expected)
                {
                    Dispatcher.UIThread.RunJobs();
                    var present = expected.HasValue;
                    var drawer = FindProbeDrawer();
                    Assert.Equal(present, drawer != null);
                    Assert.Equal(present, entity.HasComponent(PROBE_ID));
                    Assert.Equal(!present, inspector.BehaviourSelection.Any(item => item.BehaviourId == PROBE_ID));
                    var native = entityApi.GetEntityData(id)!.Behaviours;
                    Assert.Contains(native, behaviour => Equals(behaviour["REI_TYPE"], "Transform"));
                    var probe = native.Where(behaviour => Equals(behaviour["REI_TYPE"], "ProbeB")).ToArray();
                    if (!present)
                    {
                        Assert.Empty(probe);
                        return;
                    }
                    var input = drawer!.GetVisualDescendants().OfType<ReiTextBox>().Single();
                    var inspectorNumber = int.Parse(input.Text, CultureInfo.CurrentCulture);
                    var nativeNumber = Convert.ToInt32(Assert.Single(probe)["Number"]);
                    Assert.Equal((expected.Value, expected.Value), (inspectorNumber, nativeNumber));
                }
            }
            finally
            {
                api.MarkEngineStopped();
                api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
                GC.KeepAlive(createEngine);
            }
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task InspectorCheckboxClickRecordsOneUndoForButtonInteractable() => VerifyInspectorFieldAsync(SerializedTypeEnum.Boolean);

    [NativeCallbackFact]
    public Task InspectorEnumSelectionRecordsOneUndoForCameraPerspective() => VerifyInspectorFieldAsync(SerializedTypeEnum.Enum);

    [NativeCallbackFact]
    public Task ConfirmedInspectorTextRecordsOneUndoForUiText() => VerifyInspectorFieldAsync(SerializedTypeEnum.String);

    [NativeCallbackFact]
    public Task ConfirmedInspectorHexRecordsOneUndoForSpotLightColor() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom);

    [NativeCallbackFact]
    public Task InspectorColorWheelDragRecordsOneUndoForSpotLightColor() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, ColorDrag.Wheel);

    [NativeCallbackFact]
    public Task InspectorAlphaSliderDragRecordsOneUndoForSpotLightColor() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, ColorDrag.Alpha);

    [NativeCallbackTheory]
    [InlineData("R")]
    [InlineData("G")]
    [InlineData("B")]
    [InlineData("A")]
    [InlineData("H")]
    [InlineData("S")]
    [InlineData("V")]
    public Task InspectorManualColorChannelRecordsOneUndoForSpotLightColor(string channel) => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, colorChannel: channel);

    [NativeCallbackFact]
    public Task InspectorColorEditsUndoAndRedoInOrder() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, colorChannel: "H", mixedColorHistory: true);

    [NativeCallbackFact]
    public Task InspectorTextureSelectionRecordsOneUndoForImage() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, assetEdit: AssetEdit.Select);

    [NativeCallbackFact]
    public Task InspectorTextureClearRecordsOneUndoForImage() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, assetEdit: AssetEdit.Clear);

    [NativeCallbackFact]
    public Task InspectorTargetImageSelectionRecordsOneUndoForButton() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, selectComponent: true);

    [NativeCallbackTheory]
    [InlineData("X")]
    [InlineData("Y")]
    public Task InspectorReferenceResolutionEditsPreserveOtherCoordinate(string axis) => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, vectorAxis: axis);

    [NativeCallbackTheory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Z")]
    public Task InspectorProbeOffsetEditsPreserveOtherCoordinates(string axis) => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, vectorAxis: axis, editVector3: true);

    [NativeCallbackFact]
    public Task InspectorCollectionAddRecordsOneUndoAndPreservesExistingItems() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection);

    [NativeCallbackFact]
    public Task InspectorCollectionRemoveFromMiddleRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, removeCollectionItem: true);

    [NativeCallbackFact]
    public Task InspectorCollectionNumberEditRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, collectionItemValue: 42);

    [NativeCallbackFact]
    public Task InspectorCollectionEditThenRemoveSameItemUndoAndRedoInOrder() =>
        VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, removeCollectionItem: true, collectionItemValue: 42);

    [NativeCallbackFact]
    public Task InspectorCollectionEnumSelectionRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, editCollectionEnum: true);

    [NativeCallbackFact]
    public Task InspectorCollectionTextEditRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, collectionItemText: "");

    [NativeCallbackFact]
    public Task InspectorCollectionCheckboxClickRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, toggleCollectionItem: true);

    [NativeCallbackFact]
    public Task InspectorCollectionAssetSelectionRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, selectCollectionAsset: true);

    [NativeCallbackFact]
    public Task InspectorCollectionComponentSelectionRecordsOneUndo() => VerifyInspectorFieldAsync(SerializedTypeEnum.Collection, selectCollectionComponent: true);

    [NativeCallbackFact]
    public Task InspectorNestedValueEditRecordsOneUndoAndPreservesLabel() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, nestedField: NestedField.Value);

    [NativeCallbackFact]
    public Task InspectorNestedLabelEditRecordsOneUndoAndPreservesValue() => VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, nestedField: NestedField.Label);

    [NativeCallbackFact]
    public Task InspectorNestedValueThenLabelUndoAndRedoInOrder() =>
        VerifyInspectorFieldAsync(SerializedTypeEnum.Custom, nestedField: NestedField.ValueThenLabel);

    private readonly record struct ProbeSettings(int Value, string Label);
    private enum ColorDrag { None, Wheel, Alpha }
    private enum AssetEdit { None, Select, Clear }
    private enum NestedField { None, Value, Label, ValueThenLabel }

    private static Task VerifyInspectorFieldAsync(SerializedTypeEnum type, ColorDrag colorDrag = ColorDrag.None,
        string? colorChannel = null, bool mixedColorHistory = false, AssetEdit assetEdit = AssetEdit.None, bool selectComponent = false,
        string? vectorAxis = null, bool editVector3 = false, bool removeCollectionItem = false, int? collectionItemValue = null,
        bool editCollectionEnum = false, string? collectionItemText = null, bool toggleCollectionItem = false, bool selectCollectionAsset = false,
        bool selectCollectionComponent = false, NestedField nestedField = NestedField.None)
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
            var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
            Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
            var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
            using var session = factory.CreateIsolatedSession(path!);
            var api = session.EngineApi;
            var entities = new EntityApi(api);
            CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
            try
            {
                const string TEXTURE_ID = "REI_WHITE_FALLBACK_TEXTURE";
                const string TEXTURE_PATH = "C:/Assets/white.png";
                const int IMAGE_COMPONENT_ID = 7304;
                const int TRANSFORM_COMPONENT_ID = 7199;
                const int PROBE_ASSET_TYPE_ID = 7201;
                const string FIRST_ASSET_ID = "probe-first";
                const string SECOND_ASSET_ID = "probe-second";
                var isAssetField = assetEdit != AssetEdit.None;
                var editNestedSettings = nestedField != NestedField.None;
                var startEngine = isAssetField || selectComponent ? "NativeProjectStartRectMoveEngine" : "NativeProjectStartActionEngine";
                api.Invoke(typeof(StartActionEngineDelegate), startEngine, Marshal.GetFunctionPointerForDelegate(createEngine));
                api.MarkEngineStarted();
                var (componentId, componentName, field, sourceType, defaultValue) = type switch
                {
                    SerializedTypeEnum.Boolean => (7310, "Button", "_interactable", "bool", "true"),
                    SerializedTypeEnum.Enum => (7301, "Camera", "_perspective", "rei::render::CameraPerspectiveEnum", "0"),
                    SerializedTypeEnum.String => (7305, "Text", "_value", "std::string", "Text"),
                    SerializedTypeEnum.Collection when editCollectionEnum => (7101, "ProbeA", "Modes", "std::vector<rei::tests::ProbeMode>", null),
                    SerializedTypeEnum.Collection when collectionItemText != null => (7101, "ProbeA", "Labels", "std::vector<std::string>", null),
                    SerializedTypeEnum.Collection when toggleCollectionItem => (7101, "ProbeA", "Flags", "std::vector<bool>", null),
                    SerializedTypeEnum.Collection when selectCollectionAsset => (7101, "ProbeA", "Assets", "std::vector<AssetRef<ProbeDataAsset>>", null),
                    SerializedTypeEnum.Collection when selectCollectionComponent => (7101, "ProbeA", "Targets", "std::vector<ComponentRef<Transform>>", null),
                    SerializedTypeEnum.Collection => (7101, "ProbeA", "Values", "std::vector<i32>", null),
                    SerializedTypeEnum.Custom when isAssetField => (IMAGE_COMPONENT_ID, "Image", "_texture", "AssetRef<Texture>", null),
                    SerializedTypeEnum.Custom when selectComponent => (7310, "Button", "_targetImage", "ComponentRef<Image>", null),
                    SerializedTypeEnum.Custom when editVector3 => (7101, "ProbeA", "Offset", "Vector3", null),
                    SerializedTypeEnum.Custom when vectorAxis != null => (7302, "Canvas", "_referenceResolution", "Vector2", null),
                    SerializedTypeEnum.Custom when editNestedSettings => (7101, "ProbeA", "Settings", "ProbeNested", null),
                    SerializedTypeEnum.Custom => (7311, "SpotLight", "_color", "Color", (string?)null),
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };
                (object Initial, object External, object Edited) values = type switch
                {
                    SerializedTypeEnum.Boolean => (true, false, true),
                    SerializedTypeEnum.Enum => (0, 1, 0),
                    SerializedTypeEnum.String => ("Text", "From engine", ""),
                    SerializedTypeEnum.Collection when editCollectionEnum => (Array.Empty<int>(), new[] { 5, 2, 0 }, new[] { 5, 5, 0 }),
                    SerializedTypeEnum.Collection when collectionItemText != null => (Array.Empty<string>(), new[] { "Left", "Middle", "Right" },
                        new[] { "Left", collectionItemText, "Right" }),
                    SerializedTypeEnum.Collection when toggleCollectionItem => (Array.Empty<bool>(), new[] { true, false, false }, new[] { true, true, false }),
                    SerializedTypeEnum.Collection when selectCollectionAsset => (Array.Empty<string>(), new[] { FIRST_ASSET_ID, SECOND_ASSET_ID, SECOND_ASSET_ID },
                        new[] { FIRST_ASSET_ID, FIRST_ASSET_ID, SECOND_ASSET_ID }),
                    SerializedTypeEnum.Collection when selectCollectionComponent => (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>()),
                    SerializedTypeEnum.Collection when collectionItemValue is { } number => (new[] { 1, 2 }, new[] { 7, -3, 11 }, new[] { 7, number, 11 }),
                    SerializedTypeEnum.Collection when removeCollectionItem => (new[] { 1, 2 }, new[] { 7, -3, 11, 5 }, new[] { 7, 11, 5 }),
                    SerializedTypeEnum.Collection => (new[] { 1, 2 }, new[] { 7, -3, 7 }, new[] { 7, -3, 7, 0 }),
                    SerializedTypeEnum.Custom when assetEdit == AssetEdit.Clear => ("", TEXTURE_ID, ""),
                    SerializedTypeEnum.Custom when assetEdit == AssetEdit.Select => ("", "", TEXTURE_ID),
                    SerializedTypeEnum.Custom when selectComponent => (0, 0, 0),
                    SerializedTypeEnum.Custom when editVector3 => (new Vector3(1, 2, 3), new Vector3(4.25f, 5.5f, 6.75f),
                        vectorAxis switch
                        {
                            "X" => new Vector3(1200.75f, 5.5f, 6.75f),
                            "Y" => new Vector3(4.25f, 1200.75f, 6.75f),
                            "Z" => new Vector3(4.25f, 5.5f, 1200.75f),
                            _ => throw new ArgumentOutOfRangeException(nameof(vectorAxis))
                        }),
                    SerializedTypeEnum.Custom when vectorAxis != null => (new Vector2(1920, 1080), new Vector2(1600.25f, 900.5f),
                        vectorAxis == "X" ? new Vector2(1200.75f, 900.5f) : new Vector2(1600.25f, 1200.75f)),
                    SerializedTypeEnum.Custom when editNestedSettings => (new ProbeSettings(11, "nested"), new ProbeSettings(23, "Original label"),
                        nestedField == NestedField.Label ? new ProbeSettings(23, "Updated label") : new ProbeSettings(42, "Original label")),
                    SerializedTypeEnum.Custom => (new SerializedColorValue(1f, 1f, 1f, 1f),
                        new SerializedColorValue(0.123f, 0.234f, 0.345f, 0.456f), new SerializedColorValue(0.2f, 0.4f, 0.6f, 0.8f)),
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };
                var id = entities.CreateNewEntity("Inspector property history")!.EntityId!.Value;
                entities.AddBehaviour(id, componentId);
                var entity = new GameEntity(id, "Inspector property history");
                var scenes = new TestSceneManagementService(entity);
                GameEntity? image = null;
                if (selectComponent)
                {
                    var imageId = entities.CreateNewEntity("Target Image")!.EntityId!.Value;
                    entities.AddBehaviour(imageId, IMAGE_COMPONENT_ID);
                    image = new GameEntity(imageId, "Target Image");
                    image.AddBehaviour(new BehaviourComponent(IMAGE_COMPONENT_ID));
                    scenes.Scene.Value!.AddEntity(image);
                    values.Initial = values.External = id;
                    values.Edited = imageId;
                }
                if (selectCollectionComponent)
                {
                    var firstId = AddTransformTarget("First Target");
                    var secondId = AddTransformTarget("Second Target");
                    values.External = new[] { firstId, secondId, secondId };
                    values.Edited = new[] { firstId, firstId, secondId };
                }

                int AddTransformTarget(string name)
                {
                    var targetId = entities.CreateNewEntity(name)!.EntityId!.Value;
                    Assert.Contains(entities.GetEntityData(targetId)!.Behaviours, behaviour => Equals(behaviour["REI_TYPE"], "Transform"));
                    var target = new GameEntity(targetId, name);
                    target.AddBehaviour(new BehaviourComponent(TRANSFORM_COMPONENT_ID));
                    scenes.Scene.Value!.AddEntity(target);
                    return targetId;
                }
                var runner = new TestEngineRunner { ActiveMode = EngineRunMode.EditorMode };
                runner.Active.Value = runner.EditorActive.Value = true;
                var (collectionItemType, collectionItemSource) = field switch
                {
                    "Modes" => (SerializedTypeEnum.Enum, "rei::tests::ProbeMode"),
                    "Labels" => (SerializedTypeEnum.String, "std::string"),
                    "Flags" => (SerializedTypeEnum.Boolean, "bool"),
                    "Assets" => (SerializedTypeEnum.Custom, "AssetRef<ProbeDataAsset>"),
                    "Targets" => (SerializedTypeEnum.Custom, "ComponentRef<Transform>"),
                    _ => (SerializedTypeEnum.Integer, "i32")
                };
                var component = new BehaviourAssetInfo("", componentName, componentId, new ObjectFile<string>("", ""), new()
                {
                    [field] = type == SerializedTypeEnum.Collection
                        ? new(type, sourceType, collectionItemSource, collectionItemType, collectionItemSource, null, defaultValue, false)
                        : new(type, sourceType, null, SerializedTypeEnum.Invalid, null, null, defaultValue, false)
                }, [], "");
                var registry = selectComponent
                    ? new InspectorComponentRegistry(component, new BehaviourAssetInfo("", "Image", IMAGE_COMPONENT_ID, new ObjectFile<string>("", ""), new(), [], ""))
                    : selectCollectionComponent
                    ? new InspectorComponentRegistry(component, new BehaviourAssetInfo("", "Transform", TRANSFORM_COMPONENT_ID, new ObjectFile<string>("", ""), new(), [], ""))
                    : new InspectorComponentRegistry(component);
                var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
                if (editCollectionEnum)
                {
                    objects.Replace([], [new SerializableEnum
                    {
                        EnumName = "ProbeMode",
                        Options = new() { ["Cold"] = 0, ["Warm"] = 2, ["Hot"] = 5 }
                    }]);
                }
                if (type == SerializedTypeEnum.Enum)
                {
                    objects.Replace([], [new SerializableEnum
                    {
                        EnumName = "CameraPerspectiveEnum",
                        Options = new() { ["Perspective"] = 0, ["Orthographic"] = 1 }
                    }]);
                }
                if (isAssetField || selectCollectionAsset)
                {
                    var idProperty = new SerializableObjectInfo.SerializedPropertyData(SerializedTypeEnum.String, "std::string",
                        null, SerializedTypeEnum.Invalid, null, null, "", false);
                    objects.Replace([new SerializableObjectInfo("rei::assets", "AssetRef", true, new ObjectFile<string>("", ""),
                        new() { ["Id"] = idProperty }, "")], []);
                }
                else if (selectComponent || selectCollectionComponent)
                {
                    var idProperty = new SerializableObjectInfo.SerializedPropertyData(SerializedTypeEnum.Integer, "i32",
                        null, SerializedTypeEnum.Invalid, null, null, "0", false);
                    objects.Replace([new SerializableObjectInfo("rei::ecs", "ComponentRef", true, new ObjectFile<string>("", ""),
                        new() { ["SceneEntityId"] = idProperty }, "")], []);
                }
                else if (vectorAxis != null)
                {
                    var coordinate = new SerializableObjectInfo.SerializedPropertyData(SerializedTypeEnum.Float, "f32",
                        null, SerializedTypeEnum.Invalid, null, null, "0", false);
                    var coordinates = new Dictionary<string, SerializableObjectInfo.SerializedPropertyData> { ["x"] = coordinate, ["y"] = coordinate };
                    if (editVector3) coordinates.Add("z", coordinate);
                    objects.Replace([new SerializableObjectInfo("rei::math", sourceType, false, new ObjectFile<string>("", ""), coordinates, "")], []);
                }
                else if (editNestedSettings)
                {
                    objects.Replace([new SerializableObjectInfo("rei::tests", "ProbeNested", false, new ObjectFile<string>("", ""), new()
                    {
                        ["Value"] = new(SerializedTypeEnum.Integer, "i32", null, SerializedTypeEnum.Invalid, null, null, "11", false),
                        ["Label"] = new(SerializedTypeEnum.String, "std::string", null, SerializedTypeEnum.Invalid, null, null, "nested", false)
                    }, "")], []);
                }
                else if (type == SerializedTypeEnum.Custom)
                {
                    var channel = new SerializableObjectInfo.SerializedPropertyData(SerializedTypeEnum.Float, "f32",
                        null, SerializedTypeEnum.Invalid, null, null, "1", false);
                    objects.Replace([new SerializableObjectInfo("rei::render", "Color", false, new ObjectFile<string>("", ""),
                        new() { ["r"] = channel, ["g"] = channel, ["b"] = channel, ["a"] = channel }, "")], []);
                }
                var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
                var components = new BehaviourComponentsService(new TestLogger<BehaviourComponentsService>(), registry, properties);
                var syncLogger = new TestLogger<EntitySyncService>();
                var applier = new EntityStateApplier(syncLogger, registry, components, properties);
                var importer = new TestAssetImporter();
                using var sync = new EntitySyncService(importer, entities, runner, components,
                    new BehaviourSyncService(importer, runner, entities, syncLogger, applier), applier);
                var writes = 0;
                components.BehaviourPropertyChangedEvent += _ => { if (!applier.IsApplyingEngineState) writes++; };
                using var commands = new EditorCommandService();
                var command = new SetComponentPropertyCommand(commands, scenes, runner);
                var custom = new RectTransformCustomPropertiesProvider(registry, objects, null!, null!, null!, null!, scenes, null!, runner, entities, null!);
                var assets = new AssetRegistry(new TestLogger<AssetRegistry>());
                IDataAssetTypeRegistry? dataAssetTypes = null;
                if (isAssetField) assets.RegisterNewAssets([new AssetInfo(new AssetMeta(TEXTURE_ID), TEXTURE_PATH)]);
                if (selectCollectionAsset)
                {
                    var probeAsset = new SerializableObjectInfo("rei::tests", "ProbeDataAsset", false, new ObjectFile<string>("", ""), new(), "");
                    dataAssetTypes = new InspectorDataAssetTypeRegistry(new DataAssetTypeInfo(PROBE_ASSET_TYPE_ID, probeAsset));
                    assets.RegisterNewAssets([
                        new DataAssetInfo(new AssetMeta(FIRST_ASSET_ID), "C:/Assets/first.dataasset", PROBE_ASSET_TYPE_ID),
                        new DataAssetInfo(new AssetMeta(SECOND_ASSET_ID), "C:/Assets/second.dataasset", PROBE_ASSET_TYPE_ID)]);
                }
                RefreshInspector();
                using var drawer = new BehaviourComponentDrawerViewModel(entity, entity.GetBehaviour(componentId)!,
                    registry, null!, components, objects, dataAssetTypes!, new AssetSearchService(null!), assets, new AssetTypeMapper(), null!, scenes, null!, null!, custom, command);
                var value = Assert.Single(drawer.Properties);
                var theme = new FluentTheme();
                var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
                Application.Current!.Styles.Add(theme);
                Application.Current.Styles.Add(styles);
                Control view = type switch
                {
                    SerializedTypeEnum.Boolean => new BooleanPropertyView(),
                    SerializedTypeEnum.Enum => new EnumPropertyView(),
                    SerializedTypeEnum.String => new StringPropertyView(),
                    SerializedTypeEnum.Collection => new CollectionPropertyView(),
                    SerializedTypeEnum.Custom when isAssetField => new AssetPropertyView(),
                    SerializedTypeEnum.Custom when selectComponent => new ComponentRefPropertyView(),
                    SerializedTypeEnum.Custom when editVector3 => new Vector3PropertyView(),
                    SerializedTypeEnum.Custom when vectorAxis != null => new Vector2PropertyView(),
                    SerializedTypeEnum.Custom when editNestedSettings => new CustomPropertyView(),
                    SerializedTypeEnum.Custom => new ColorPropertyView(),
                    _ => throw new ArgumentOutOfRangeException(nameof(type))
                };
                if (value is CollectionPropertyViewModel collection) collection.SwitchExpandState();
                if (value is CustomPropertyViewModel nested) nested.SwitchExpandState();
                view.DataContext = value;
                var window = new Window { Content = view, Width = 400, Height = type is SerializedTypeEnum.Custom or SerializedTypeEnum.Collection ? 480 : 140 };
                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    var toggle = view.GetVisualDescendants().OfType<CheckBox>().SingleOrDefault();
                    var combo = view.GetVisualDescendants().OfType<ComboBox>().SingleOrDefault();
                    var textInputs = view.GetVisualDescendants().OfType<ReiTextBox>().ToArray();
                    var nestedValueInput = editNestedSettings ? view.GetVisualDescendants().OfType<IntegerPropertyView>().Single()
                        .GetVisualDescendants().OfType<ReiTextBox>().Single() : null;
                    var nestedLabelInput = editNestedSettings ? view.GetVisualDescendants().OfType<StringPropertyView>().Single()
                        .GetVisualDescendants().OfType<ReiTextBox>().Single() : null;
                    var text = vectorAxis switch
                    {
                        "X" => textInputs[0],
                        "Y" => textInputs[1],
                        "Z" => textInputs[2],
                        _ when editNestedSettings => nestedField == NestedField.Label ? nestedLabelInput : nestedValueInput,
                        _ when type == SerializedTypeEnum.Collection => null,
                        _ => textInputs.SingleOrDefault()
                    };
                    ReiTextBox? channelInput = null;
                    ColorWheelPicker? wheel = null;
                    Slider? alpha = null;
                    if (view is ColorPropertyView)
                    {
                        view.FindControl<Button>("HexButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                        Dispatcher.UIThread.RunJobs();
                        Assert.True(view.Resources.TryGetResource("ColorEditorFlyout", null, out var resource));
                        var content = (Control)Assert.IsType<Flyout>(resource).Content!;
                        text = content.GetLogicalDescendants().OfType<ReiTextBox>().Single(input => input.Name == "HexInput");
                        if (colorChannel != null)
                            channelInput = content.GetLogicalDescendants().OfType<ReiTextBox>().Single(input => input.Name == colorChannel + "Input");
                        wheel = content.GetLogicalDescendants().OfType<ColorWheelPicker>().Single();
                        alpha = content.GetLogicalDescendants().OfType<Slider>().Single(slider => slider.Name == "AlphaSlider");
                    }
                    AssertValues(values.Initial);
                    if (colorDrag == ColorDrag.Wheel)
                    {
                        // With zero saturation, turning the hue ring leaves the stored RGBA unchanged.
                        var picker = wheel!;
                        var surface = TopLevel.GetTopLevel(picker)!;
                        var center = new Point(picker.Bounds.Width / 2, picker.Bounds.Height / 2);
                        var start = picker.TranslatePoint(center + new Vector(95, 0), surface)!.Value;
                        var end = picker.TranslatePoint(center + new Vector(0, 95), surface)!.Value;
                        surface.MouseDown(start, MouseButton.Left);
                        surface.MouseMove(end);
                        surface.MouseUp(end, MouseButton.Left);
                        Dispatcher.UIThread.RunJobs();
                        Assert.True(picker.Hue > 0);
                        AssertValues(values.Initial);
                        Assert.False(commands.CanUndo);
                    }
                    var external = values.External switch
                    {
                        SerializedColorValue rgba => new { r = new { Value = rgba.R }, g = new { Value = rgba.G }, b = new { Value = rgba.B }, a = new { Value = rgba.A } },
                        string idValue when isAssetField => new { Id = new { Value = idValue } },
                        string[] assetIds when selectCollectionAsset => assetIds.Select(assetId => new { Id = new { Value = assetId } }).ToArray(),
                        int sceneEntityId when selectComponent => new { SceneEntityId = new { Value = sceneEntityId } },
                        int[] targetIds when selectCollectionComponent => targetIds.Select(targetId => new { SceneEntityId = new { Value = targetId } }).ToArray(),
                        Vector2 vector => new { x = new { Value = vector.X }, y = new { Value = vector.Y } },
                        Vector3 vector => new { x = new { Value = vector.X }, y = new { Value = vector.Y }, z = new { Value = vector.Z } },
                        ProbeSettings settings => new { Value = new { Value = settings.Value }, Label = new { Value = settings.Label } },
                        _ => values.External
                    };
                    entities.SetData(new SetEntityDataRequest
                    {
                        SceneId = id,
                        Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = componentId,
                            [field] = new { Value = external } }]
                    });
                    RefreshInspector();
                    AssertValues(values.External);
                    Assert.False(commands.CanUndo);
                    Assert.False(commands.CanRedo);
                    Assert.Equal(0, writes);

                    if (colorDrag != ColorDrag.None)
                    {
                        values.Edited = DragColor(colorDrag, (SerializedColorValue)values.External);
                    }
                    else if (channelInput != null)
                    {
                        var editedChannel = colorChannel == "H" ? 120f : 0.75f;
                        var before = (SerializedColorValue)values.External;
                        values.Edited = colorChannel switch
                        {
                            "R" => before with { R = editedChannel },
                            "G" => before with { G = editedChannel },
                            "B" => before with { B = editedChannel },
                            "A" => before with { A = editedChannel },
                            "H" or "S" or "V" => EditHsvChannel(before, colorChannel, editedChannel),
                            _ => throw new ArgumentOutOfRangeException(nameof(colorChannel))
                        };
                        channelInput.TextInternal = editedChannel.ToString(CultureInfo.CurrentCulture);
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);
                        Assert.Equal(0, writes);
                        channelInput.LoseFocus();
                    }
                    else if (assetEdit == AssetEdit.Clear)
                    {
                        var pickerView = view.GetVisualDescendants().OfType<AssetPickerView>().Single();
                        pickerView.FindControl<Border>("LabelBorder")!.RaiseEvent(new KeyEventArgs
                        {
                            RoutedEvent = InputElement.KeyDownEvent,
                            Key = Key.Delete
                        });
                    }
                    else if (value is AssetPropertyViewModel asset)
                    {
                        var picker = asset.AssetPicker!;
                        picker.RefreshSearchResultsForAll();
                        Assert.Single(picker.SearchResults).SelectCommand.Execute(null);
                    }
                    else if (value is ComponentRefPropertyViewModel reference)
                    {
                        var picker = reference.Picker!;
                        picker.RefreshSearchResultsForAll();
                        var targetId = image!.Id.ToString(CultureInfo.InvariantCulture);
                        Assert.Single(picker.SearchResults, item => item.AssetId == targetId).SelectCommand.Execute(null);
                    }
                    else if (editNestedSettings || vectorAxis != null)
                    {
                        text!.TextInternal = values.Edited is ProbeSettings settings
                            ? nestedField == NestedField.Label ? settings.Label : settings.Value.ToString(CultureInfo.CurrentCulture)
                            : 1200.75f.ToString(CultureInfo.CurrentCulture);
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);
                        Assert.Equal(0, writes);
                        text.LoseFocus();
                    }
                    else if (collectionItemText != null || collectionItemValue.HasValue)
                    {
                        components.RefreshComponents(entity);
                        text = view.GetVisualDescendants().OfType<ReiTextBox>().ElementAt(1);
                        text.TextInternal = collectionItemText ?? collectionItemValue.GetValueOrDefault().ToString(CultureInfo.CurrentCulture);
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);
                        Assert.Equal(0, writes);
                        text.LoseFocus();
                    }
                    else if (editCollectionEnum)
                    {
                        var mode = view.GetVisualDescendants().OfType<ComboBox>().ElementAt(1);
                        mode.Focus();
                        mode.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
                    }
                    else if (toggleCollectionItem)
                    {
                        Click(view.GetVisualDescendants().OfType<CheckBox>().ElementAt(1));
                    }
                    else if (selectCollectionAsset)
                    {
                        var assetList = Assert.IsType<CollectionPropertyViewModel>(value);
                        var picker = Assert.IsType<AssetPropertyViewModel>(Assert.Single(assetList.Value[1].Value)).AssetPicker!;
                        picker.RefreshSearchResultsForAll();
                        Assert.Single(picker.SearchResults, item => item.AssetId == FIRST_ASSET_ID).SelectCommand.Execute(null);
                    }
                    else if (selectCollectionComponent)
                    {
                        var refs = Assert.IsType<CollectionPropertyViewModel>(value);
                        var picker = Assert.IsType<ComponentRefPropertyViewModel>(Assert.Single(refs.Value[1].Value)).Picker!;
                        var targetId = ((int[])values.Edited)[1].ToString(CultureInfo.InvariantCulture);
                        picker.RefreshSearchResultsForAll();
                        Assert.Single(picker.SearchResults, item => item.AssetId == targetId).SelectCommand.Execute(null);
                    }
                    else if (value is CollectionPropertyViewModel list)
                    {
                        ClickCollectionButton(removeCollectionItem ? list.Value[1].RemoveCommand : list.AddItemCommand);
                    }
                    else if (text != null)
                    {
                        if (type == SerializedTypeEnum.Custom)
                        {
                            text.TextInternal = "invalid";
                            text.LoseFocus();
                            AssertValues(values.External);
                            Assert.False(commands.CanUndo);
                            Assert.Equal(0, writes);
                        }
                        text.TextInternal = values.Edited is SerializedColorValue color ? ColorHex(color) : (string)values.Edited;
                        Dispatcher.UIThread.RunJobs();
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);
                        text.LoseFocus();
                    }
                    else if (combo != null)
                    {
                        combo.Focus();
                        combo.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
                    }
                    else
                    {
                        Click(Assert.IsType<CheckBox>(toggle));
                    }
                    Dispatcher.UIThread.RunJobs();
                    AssertValues(values.Edited);
                    Assert.True(commands.CanUndo);
                    var editWrites = colorDrag != ColorDrag.None ? writes : 1;
                    Assert.Equal(editWrites, writes);

                    if (nestedField == NestedField.ValueThenLabel)
                    {
                        var afterLabel = ((ProbeSettings)values.Edited) with { Label = "Updated label" };
                        nestedLabelInput!.TextInternal = afterLabel.Label;
                        nestedLabelInput.LoseFocus();
                        AssertValues(afterLabel);
                        Assert.Equal(2, writes);

                        await commands.UndoAsync();
                        AssertValues(values.Edited);
                        await commands.UndoAsync();
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);

                        await commands.RedoAsync();
                        AssertValues(values.Edited);
                        await commands.RedoAsync();
                        AssertValues(afterLabel);
                        Assert.False(commands.CanRedo);
                        Assert.Equal(6, writes);
                        Assert.Empty(syncLogger.Entries);
                        return true;
                    }

                    if (removeCollectionItem && collectionItemValue.HasValue)
                    {
                        var afterRemoval = new[] { 7, 11 };
                        ClickCollectionButton(Assert.IsType<CollectionPropertyViewModel>(value).Value[1].RemoveCommand);
                        AssertValues(afterRemoval);
                        Assert.Equal(2, writes);

                        await commands.UndoAsync();
                        AssertValues(values.Edited);
                        await commands.UndoAsync();
                        AssertValues(values.External);
                        Assert.False(commands.CanUndo);

                        await commands.RedoAsync();
                        AssertValues(values.Edited);
                        await commands.RedoAsync();
                        AssertValues(afterRemoval);
                        Assert.False(commands.CanRedo);
                        Assert.Equal(6, writes);
                        Assert.Empty(syncLogger.Entries);
                        return true;
                    }

                    if (mixedColorHistory)
                    {
                        var afterInput = (SerializedColorValue)values.Edited;
                        var afterWheel = DragColor(ColorDrag.Wheel, afterInput);
                        AssertValues(afterWheel);
                        var afterAlpha = DragColor(ColorDrag.Alpha, afterWheel);
                        AssertValues(afterAlpha);
                        var writesBeforeReplay = writes;

                        foreach (var expected in new[] { afterWheel, afterInput, (SerializedColorValue)values.External })
                        {
                            Assert.True(commands.CanUndo);
                            await commands.UndoAsync();
                            AssertValues(expected);
                        }
                        Assert.False(commands.CanUndo);

                        foreach (var expected in new[] { afterInput, afterWheel, afterAlpha })
                        {
                            Assert.True(commands.CanRedo);
                            await commands.RedoAsync();
                            AssertValues(expected);
                        }
                        Assert.False(commands.CanRedo);
                        Assert.Equal(writesBeforeReplay + 6, writes);
                        Assert.Empty(syncLogger.Entries);
                        return true;
                    }

                    await commands.UndoAsync();
                    AssertValues(values.External);
                    Assert.False(commands.CanUndo);
                    Assert.True(commands.CanRedo);
                    Assert.Equal(editWrites + 1, writes);
                    (channelInput ?? text)?.LoseFocus();
                    Assert.False(commands.CanUndo);
                    Assert.True(commands.CanRedo);
                    await commands.RedoAsync();
                    AssertValues(values.Edited);
                    Assert.False(commands.CanRedo);
                    Assert.Equal(editWrites + 2, writes);
                    Assert.Empty(syncLogger.Entries);

                    void ClickCollectionButton(System.Windows.Input.ICommand commandToClick)
                    {
                        var button = view.GetVisualDescendants().OfType<Button>().Single(button => ReferenceEquals(button.Command, commandToClick));
                        Click(button);
                    }

                    void Click(Control control)
                    {
                        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
                        window.MouseDown(center, MouseButton.Left);
                        window.MouseUp(center, MouseButton.Left);
                    }

                    SerializedColorValue DragColor(ColorDrag drag, SerializedColorValue before)
                    {
                        Control target = drag == ColorDrag.Wheel ? wheel! : alpha!.GetVisualDescendants().OfType<Thumb>().Single();
                        var surface = TopLevel.GetTopLevel(target)!;
                        var start = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), surface)!.Value;
                        var step = drag == ColorDrag.Wheel ? new Vector(12, 10) : new Vector(30, 0);
                        surface.MouseMove(start);
                        Dispatcher.UIThread.RunJobs();
                        surface.MouseDown(start, MouseButton.Left);
                        Dispatcher.UIThread.RunJobs();
                        surface.MouseMove(start + step);
                        Dispatcher.UIThread.RunJobs();
                        var preview = ReadNativeColor(ReadNativeComponent()[field]);
                        Assert.True(before != preview,
                            $"Drag did not update native color. Thumb/target bounds: {target.Bounds}, start: {start}, slider: {alpha?.Value}.");
                        AssertValues(preview);
                        Assert.False(commands.CanUndo);

                        var end = start + step * 2;
                        surface.MouseMove(end);
                        Dispatcher.UIThread.RunJobs();
                        var final = ReadNativeColor(ReadNativeComponent()[field]);
                        Assert.NotEqual(preview, final);
                        if (drag == ColorDrag.Wheel) Assert.Equal(before.A, final.A);
                        else
                        {
                            Assert.NotEqual(before.A, final.A);
                            Assert.Equal(before with { A = final.A }, final);
                        }
                        Assert.False(commands.CanUndo);
                        var previewWrites = writes;
                        surface.MouseUp(end, MouseButton.Left);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Equal(previewWrites, writes);
                        return final;
                    }

                    void AssertValues(object expected)
                    {
                        Dispatcher.UIThread.RunJobs();
                        var native = ReadNativeComponent()[field];
                        switch (value)
                        {
                            case BooleanPropertyViewModel flag:
                                Assert.Equal((bool)expected, toggle!.IsChecked);
                                Assert.Equal(expected, flag.Value);
                                Assert.Equal(expected, native);
                                break;
                            case EnumPropertyViewModel choice:
                                var label = (int)expected == 0 ? "Perspective" : "Orthographic";
                                Assert.Equal(label, combo!.SelectedItem);
                                Assert.Equal(label, choice.SelectedValue);
                                Assert.Equal(expected, choice.Value);
                                Assert.Equal(choice.Value, Convert.ToInt32(native));
                                break;
                            case StringPropertyViewModel stringValue:
                                Assert.Equal((string)expected, text!.Text);
                                Assert.Equal(expected, stringValue.Value);
                                Assert.Equal(expected, native);
                                break;
                            case ColorPropertyViewModel color:
                                var expectedColor = (SerializedColorValue)expected;
                                Assert.Equal(ColorHex(expectedColor), text!.Text);
                                Assert.Equal(expectedColor, new SerializedColorValue(color.R, color.G, color.B, color.A));
                                Assert.Equal(expectedColor, ReadNativeColor(native));
                                break;
                            case AssetPropertyViewModel asset:
                                var picker = asset.AssetPicker!;
                                Assert.Equal((string)expected, picker.SelectedAssetId);
                                var expectedLabel = Equals(expected, TEXTURE_ID) ? "white" : "empty";
                                Assert.Equal(expectedLabel, view.GetVisualDescendants().OfType<Label>().Single().Content);
                                Assert.Equal((string)expected, JObject.FromObject(native).Value<string>("Id"));
                                break;
                            case ComponentRefPropertyViewModel reference:
                                var expectedId = (int)expected;
                                var expectedEntity = expectedId == entity.Id ? entity : image!;
                                Assert.Equal(expectedId.ToString(CultureInfo.InvariantCulture), reference.Picker!.SelectedAssetId);
                                Assert.Equal($"{expectedEntity.Name} ({expectedId})", view.GetVisualDescendants().OfType<Label>().Single().Content);
                                Assert.Equal(expectedId, JObject.FromObject(native).Value<int>("SceneEntityId"));
                                break;
                            case Vector2PropertyViewModel vector:
                                var expectedVector = (Vector2)expected;
                                Assert.Equal(expectedVector, new Vector2(vector.X, vector.Y));
                                Assert.Equal(expectedVector.X, float.Parse(textInputs[0].Text, CultureInfo.CurrentCulture));
                                Assert.Equal(expectedVector.Y, float.Parse(textInputs[1].Text, CultureInfo.CurrentCulture));
                                var nativeVector = JObject.FromObject(native);
                                Assert.Equal(expectedVector, new Vector2(nativeVector.Value<float>("x"), nativeVector.Value<float>("y")));
                                break;
                            case Vector3PropertyViewModel vector:
                                var expectedVector3 = (Vector3)expected;
                                Assert.Equal(expectedVector3, new Vector3(vector.X, vector.Y, vector.Z));
                                Assert.Equal(expectedVector3.X, float.Parse(textInputs[0].Text, CultureInfo.CurrentCulture));
                                Assert.Equal(expectedVector3.Y, float.Parse(textInputs[1].Text, CultureInfo.CurrentCulture));
                                Assert.Equal(expectedVector3.Z, float.Parse(textInputs[2].Text, CultureInfo.CurrentCulture));
                                var nativeVector3 = JObject.FromObject(native);
                                Assert.Equal(expectedVector3, new Vector3(nativeVector3.Value<float>("x"), nativeVector3.Value<float>("y"), nativeVector3.Value<float>("z")));
                                break;
                            case CustomPropertyViewModel:
                                var expectedSettings = (ProbeSettings)expected;
                                Assert.Equal(expectedSettings.Value, int.Parse(nestedValueInput!.Text, CultureInfo.CurrentCulture));
                                Assert.Equal(expectedSettings.Label, nestedLabelInput!.Text);
                                var nativeSettings = JObject.FromObject(native);
                                Assert.Equal(expectedSettings, new ProbeSettings(nativeSettings.Value<int>("Value"), nativeSettings.Value<string>("Label")!));
                                break;
                            case CollectionPropertyViewModel collectionViewModel:
                                Assert.Equal(((Array)expected).Length, collectionViewModel.Count);
                                Assert.Equal(Enumerable.Range(0, collectionViewModel.Count).Select(index => $"[{index}]"),
                                    collectionViewModel.Value.Select(item => item.Property.Name));
                                if (selectCollectionAsset)
                                {
                                    var assetIds = (string[])expected;
                                    Assert.Equal(assetIds, collectionViewModel.Value.Select(row =>
                                        Assert.IsType<AssetPropertyViewModel>(Assert.Single(row.Value)).AssetPicker!.SelectedAssetId));
                                    Assert.Equal(assetIds.Select(assetId => assetId == FIRST_ASSET_ID ? "first" : "second"),
                                        view.GetVisualDescendants().OfType<AssetPickerView>().Select(picker =>
                                            Assert.IsType<string>(picker.GetVisualDescendants().OfType<Label>().Single().Content)));
                                    Assert.Equal(assetIds, JArray.FromObject(native).Select(item => item.Value<string>("Id")));
                                    break;
                                }
                                if (expected is string[] labels)
                                {
                                    Assert.Equal(labels, view.GetVisualDescendants().OfType<ReiTextBox>().Select(input => input.Text));
                                    Assert.Equal(labels, JArray.FromObject(native).ToObject<string[]>());
                                    break;
                                }
                                if (expected is bool[] flags)
                                {
                                    Assert.Equal(flags.Select(flag => (bool?)flag),
                                        view.GetVisualDescendants().OfType<CheckBox>().Select(input => input.IsChecked));
                                    Assert.Equal(flags, JArray.FromObject(native).ToObject<bool[]>());
                                    break;
                                }
                                var expectedItems = (int[])expected;
                                if (selectCollectionComponent)
                                {
                                    Assert.Equal(expectedItems.Select(targetId => targetId.ToString(CultureInfo.InvariantCulture)),
                                        collectionViewModel.Value.Select(row => Assert.IsType<ComponentRefPropertyViewModel>(Assert.Single(row.Value)).Picker!.SelectedAssetId));
                                    Assert.Equal(expectedItems.Select(targetId => $"{scenes.Scene.Value!.GetById(targetId)!.Name} ({targetId})"),
                                        view.GetVisualDescendants().OfType<AssetPickerView>().Select(picker =>
                                            Assert.IsType<string>(picker.GetVisualDescendants().OfType<Label>().Single().Content)));
                                    Assert.Equal(expectedItems, JArray.FromObject(native).Select(item => item.Value<int>("SceneEntityId")));
                                    break;
                                }
                                if (editCollectionEnum)
                                {
                                    var modes = objects.GetEnum("ProbeMode")!.Options;
                                    Assert.Equal(expectedItems.Select(item => modes.Single(mode => mode.Value == item).Key),
                                        view.GetVisualDescendants().OfType<ComboBox>().Select(input => (string?)input.SelectedItem));
                                }
                                else
                                {
                                    Assert.Equal(expectedItems, view.GetVisualDescendants().OfType<ReiTextBox>()
                                        .Select(input => int.Parse(input.Text, CultureInfo.CurrentCulture)));
                                }
                                Assert.Equal(expectedItems, JArray.FromObject(native).ToObject<int[]>());
                                break;
                            default:
                                throw new InvalidOperationException("Unexpected Inspector property editor.");
                        }
                    }
                }
                finally
                {
                    window.Close();
                    Application.Current.Styles.Remove(styles);
                    Application.Current.Styles.Remove(theme);
                }

                static string ColorHex(SerializedColorValue color) => ColorConversionUtility.ToHex(color.R, color.G, color.B, color.A);

                static SerializedColorValue EditHsvChannel(SerializedColorValue before, string channel, float value)
                {
                    ColorConversionUtility.RgbToHsv(before.R, before.G, before.B, out var h, out var s, out var v);
                    ColorConversionUtility.HsvToRgb(channel == "H" ? value : h, channel == "S" ? value : s,
                        channel == "V" ? value : v, out var r, out var g, out var b);
                    return before with { R = r, G = g, B = b };
                }

                static SerializedColorValue ReadNativeColor(object value)
                {
                    var channels = JObject.FromObject(value);
                    return new(channels.Value<float>("r"), channels.Value<float>("g"), channels.Value<float>("b"), channels.Value<float>("a"));
                }

                Dictionary<string, object> ReadNativeComponent() =>
                    entities.GetEntityData(id)!.Behaviours.Single(behaviour => Equals(behaviour["REI_TYPE"], componentName));

                void RefreshInspector()
                {
                    var behaviours = entities.GetEntityData(id)!.Behaviours
                        .Where(behaviour => Equals(behaviour["REI_TYPE"], componentName) || selectComponent && Equals(behaviour["REI_TYPE"], "Image"))
                        .ToList();
                    applier.Apply(entity, new GetEntityDataResponse { Name = entity.Name, Behaviours = behaviours });
                    Dispatcher.UIThread.RunJobs();
                }
            }
            finally
            {
                api.MarkEngineStopped();
                api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
                GC.KeepAlive(createEngine);
            }
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task ConfirmedNumericInspectorEditsUndoAndRedoAcrossComponents()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
            var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
            Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
            var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
            using var session = factory.CreateIsolatedSession(path!);
            var api = session.EngineApi;
            var entities = new EntityApi(api);
            CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
            try
            {
                api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
                api.MarkEngineStarted();
                const int SPOT_LIGHT = 7311;
                const int PROBE = 7101;
                var id = entities.CreateNewEntity("Numeric history")!.EntityId!.Value;
                entities.AddBehaviour(id, SPOT_LIGHT);
                entities.AddBehaviour(id, PROBE);
                var entity = new GameEntity(id, "Numeric history");
                var scenes = new TestSceneManagementService(entity);
                var runner = new TestEngineRunner { ActiveMode = EngineRunMode.EditorMode };
                runner.Active.Value = runner.EditorActive.Value = true;
                var registry = new InspectorComponentRegistry(
                    Schema("SpotLight", SPOT_LIGHT, "_range", SerializedTypeEnum.Float, "f32"),
                    Schema("ProbeA", PROBE, "Number", SerializedTypeEnum.Integer, "i32"));
                var objects = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
                var properties = new SerializedPropertiesService(objects, new TestLogger<SerializedPropertiesService>());
                var components = new BehaviourComponentsService(new TestLogger<BehaviourComponentsService>(), registry, properties);
                var syncLogger = new TestLogger<EntitySyncService>();
                var applier = new EntityStateApplier(syncLogger, registry, components, properties);
                var importer = new TestAssetImporter();
                using var sync = new EntitySyncService(importer, entities, runner, components,
                    new BehaviourSyncService(importer, runner, entities, syncLogger, applier), applier);
                using var commands = new EditorCommandService();
                var command = new SetComponentPropertyCommand(commands, scenes, runner);
                var custom = new RectTransformCustomPropertiesProvider(registry, objects, null!, null!, null!, null!, scenes, null!, runner, entities, null!);
                RefreshFromNative();
                using var spot = Drawer(SPOT_LIGHT);
                using var number = Drawer(PROBE);
                var spotValue = Assert.IsType<FloatPropertyViewModel>(Assert.Single(spot.Properties));
                var numberValue = Assert.IsType<IntegerPropertyViewModel>(Assert.Single(number.Properties));
                var rangeInput = new ReiTextBox { DataContext = spotValue, IsFloat = true };
                var numberInput = new ReiTextBox { DataContext = numberValue };
                using var rangeBinding = rangeInput.Bind(ReiTextBox.TextProperty, new Binding("Value") { Mode = BindingMode.TwoWay });
                using var numberBinding = numberInput.Bind(ReiTextBox.TextProperty, new Binding("Value") { Mode = BindingMode.TwoWay });
                Dispatcher.UIThread.RunJobs();

                entities.SetData(new SetEntityDataRequest
                {
                    SceneId = id,
                    Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = SPOT_LIGHT, ["_range"] = new { Value = 12f } }]
                });
                RefreshFromNative();
                AssertValues(12f, 7);
                Assert.False(commands.CanUndo);

                rangeInput.TextInternal = 6.25f.ToString(CultureInfo.CurrentCulture);
                numberInput.TextInternal = "23";
                AssertValues(12f, 7);
                Assert.False(commands.CanUndo);
                rangeInput.LoseFocus();
                AssertValues(6.25f, 7);
                numberInput.LoseFocus();
                AssertValues(6.25f, 23);

                await commands.UndoAsync();
                AssertValues(6.25f, 7);
                await commands.UndoAsync();
                AssertValues(12f, 7);
                Assert.False(commands.CanUndo);
                Assert.True(commands.CanRedo);
                rangeInput.LoseFocus();
                Assert.True(commands.CanRedo);
                Assert.False(commands.CanUndo);

                await commands.RedoAsync();
                AssertValues(6.25f, 7);
                await commands.RedoAsync();
                AssertValues(6.25f, 23);
                Assert.False(commands.CanRedo);

                Assert.True(components.DeleteComponent(entity, number.BehaviourComponent));
                Assert.False(commands.CanUndo);
                Assert.Empty(syncLogger.Entries);

                BehaviourComponentDrawerViewModel Drawer(int componentId) => new(entity, entity.GetBehaviour(componentId)!,
                    registry, null!, components, objects, null!, null!, null!, null!, null!, scenes, null!, null!, custom, command);

                void RefreshFromNative()
                {
                    var state = entities.GetEntityData(id)!;
                    state.Behaviours = state.Behaviours.Where(behaviour => registry.GetIdByName((string)behaviour["REI_TYPE"]) != null).ToList();
                    applier.Apply(entity, state);
                    Dispatcher.UIThread.RunJobs();
                }

                void AssertValues(float range, int integer)
                {
                    Assert.Equal(range, spotValue.Value);
                    Assert.Equal(integer, numberValue.Value);
                    var native = entities.GetEntityData(id)!.Behaviours;
                    Assert.Equal(range, Convert.ToSingle(native.Single(behaviour => Equals(behaviour["REI_TYPE"], "SpotLight"))["_range"]));
                    Assert.Equal(integer, Convert.ToInt32(native.Single(behaviour => Equals(behaviour["REI_TYPE"], "ProbeA"))["Number"]));
                }
            }
            finally
            {
                api.MarkEngineStopped();
                api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
                GC.KeepAlive(createEngine);
            }
            return true;
        }, CancellationToken.None);

        static BehaviourAssetInfo Schema(string name, int id, string field, SerializedTypeEnum type, string sourceType) =>
            new("", name, id, new ObjectFile<string>("", ""), new()
            {
                [field] = new(type, sourceType, null, SerializedTypeEnum.Invalid, null, null, "0", false)
            }, [], "");
    }

    [NativeCallbackFact]
    public Task NativeCallbacksRecordTransformActionsInCommonHistoryAndReplayWithoutRecordingAgain()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var mode in new[] { 0, 1, 2 })
            {
                foreach (var count in new[] { 1, 3 }) await VerifyNativeCallbackHistoryAsync(mode, count);
            }
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyNativeCallbackHistoryAsync(int mode, int targetCount)
    {
        Assert.True(Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD") == "1",
            "Run Area=EngineActionCallbacks in a dedicated testhost with REI_NATIVE_TEST_CHILD=1; the native fixture changes process globals.");
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build Rei/tests/ProjectDll/NativeProject.vcxproj and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var id = api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", mode, targetCount);
            Assert.NotEqual(0UL, id);
            Assert.Equal(new[] { id }, received);
            AssertTransforms(applied: true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(commands.CanUndo);
            await commands.UndoAsync();
            AssertTransforms(applied: false);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            AssertTransforms(applied: true);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanRedo);
            foreach (var missing in new[] { 0UL, ulong.MaxValue })
            {
                Assert.False(api.UndoEditorAction(missing));
                Assert.False(api.RedoEditorAction(missing));
            }
            AssertTransforms(applied: true);
            Assert.Equal(new[] { id }, received);
            Assert.Empty(logger.Entries);
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
        Assert.False(api.UndoEditorAction(1));
        Assert.False(api.RedoEditorAction(1));

        void AssertTransforms(bool applied)
        {
            for (var index = 0; index < targetCount; index++)
            {
                float[] expected = [1 + index, 2 + index, 3 + index, 1 + index, 1 + index, 1 + index, 0, 0, 0, 1];
                if (applied)
                {
                    if (mode == 0) { expected[0] += 3; expected[1] += 3; expected[2] += 3; }
                    else if (mode == 1) { expected[3] += 1; expected[4] += 2; expected[5] += 3; }
                    else { expected[8] = MathF.Sqrt(0.5f); expected[9] = MathF.Sqrt(0.5f); }
                }
                var values = new float[10];
                api.Invoke(typeof(ReadTransformDelegate), "NativeProjectReadActionTransform", index, (object)values);
                for (var i = 0; i < values.Length; i++)
                    Assert.True(MathF.Abs(expected[i] - values[i]) < 0.00001f, $"Mode {mode}, target {index}, component {i}: expected {expected[i]}, actual {values[i]}.");
            }
        }
    }

    [NativeCallbackFact]
    public Task InspectorTransformConfirmationsShareHistoryWithNativeActionAndRename()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var property in new[] { "_position", "_scale", "_rotation" }) await VerifyInspectorTransformAsync(property);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorTransformAsync(string propertyName)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entityApi = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var entityId = entityApi.CreateNewEntity("Inspector Transform")?.EntityId ?? throw new InvalidOperationException("Could not create native entity.");
            using var inspector = new InspectorTransformFixture(entityApi, entityId);
            var initial = propertyName == "_scale" ? 1f : 0f;
            var changed = propertyName == "_scale" ? -2.5f : propertyName == "_rotation" ? 45f : 12f;
            using var vm = new Vector3PropertyViewModel(inspector.Transform.GetProperty(propertyName));
            RefreshAndCheckTransform(initial);
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding("X") { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.TextInternal = changed.ToString(System.Globalization.CultureInfo.CurrentCulture);
            RefreshAndCheckTransform(initial);
            Assert.Empty(received);
            input.LoseFocus();
            RefreshAndCheckTransform(changed);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanUndo);
            var inspectorActionId = received[0];

            await commands.UndoAsync();
            RefreshAndCheckTransform(initial);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            input.TextInternal = initial.ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            // Also verify the native no-op guard when a caller explicitly sends the unchanged value.
            entityApi.SetData(new SetEntityDataRequest
            {
                SceneId = entityId,
                RecordTransformChange = true,
                Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = InspectorTransformFixture.TRANSFORM_ID,
                    [propertyName] = SerializedPropertyJsonConverter.SerializeRuntimeProperty(inspector.Transform.GetProperty(propertyName)) }]
            });
            RefreshAndCheckTransform(initial);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            RefreshAndCheckTransform(changed);

            var nativeActionId = api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", 0, 1);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { inspectorActionId, nativeActionId }, received);
            var entities = new TestEntityManagementService { OnRename = (entity, name) => { entityApi.Rename(entity.Id, name); entity.SetName(name); } };
            var scenes = new TestSceneManagementService(inspector.Entity);
            var rename = new EntityRenameCommand(entities, new TestLogger<EntityRenameCommand>(), scenes, inspector.Runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(inspector.Entity, "Renamed"));
            Assert.Equal("Renamed", entityApi.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Inspector Transform", entityApi.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            AssertNativeActionPosition(1, 2, 3);
            RefreshAndCheckTransform(changed);
            await commands.UndoAsync();
            RefreshAndCheckTransform(initial);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            await commands.RedoAsync();
            RefreshAndCheckTransform(changed);
            await commands.RedoAsync();
            AssertNativeActionPosition(4, 5, 6);
            await commands.RedoAsync();
            Assert.Equal("Renamed", entityApi.GetEntityData(entityId)!.Name);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanRedo);
            Assert.Equal(new[] { inspectorActionId, nativeActionId }, received);
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void RefreshAndCheckTransform(float expectedX)
            {
                var state = entityApi.GetEntityData(entityId)!;
                Assert.NotNull(state);
                var transform = state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Transform");
                inspector.Apply(state);
                foreach (var name in new[] { "_position", "_scale", "_rotation" })
                {
                    var native = JObject.FromObject(transform[name]);
                    var editor = (Dictionary<string, SerializedProperty>)inspector.Transform.GetProperty(name).Value!;
                    foreach (var axis in new[] { "x", "y", "z" })
                    {
                        var expected = name == propertyName && axis == "x" ? expectedX : name == "_scale" ? 1f : 0f;
                        Assert.InRange(MathF.Abs(native[axis]!.Value<float>() - expected), 0f, 0.0001f);
                        Assert.InRange(MathF.Abs(Convert.ToSingle(editor[axis].Value) - expected), 0f, 0.0001f);
                    }
                }
                Assert.InRange(MathF.Abs(vm.X - expectedX), 0f, 0.0001f);
            }

            void AssertNativeActionPosition(float x, float y, float z)
            {
                var values = new float[10];
                api.Invoke(typeof(ReadTransformDelegate), "NativeProjectReadActionTransform", 0, (object)values);
                Assert.Equal(new[] { x, y, z }, values.Take(3));
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task HierarchyDragSharesHistoryWithTransformAndRename()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var nested in new[] { false, true }) await VerifyHierarchyDragAsync(nested);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task GroupedHierarchyDragRecordsOneEntryAndPreservesSiblingOrder()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var nested in new[] { false, true })
                foreach (var disjoint in new[] { false, true }) await VerifyHierarchyDragAsync(nested, grouped: true, disjoint: disjoint);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyHierarchyDragAsync(bool nested, bool grouped = false, bool disjoint = false)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entityApi = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        var editors = new List<InspectorTransformFixture>();
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var parentId = nested ? entityApi.CreateNewEntity("Parent")!.EntityId!.Value : 0;
            var hierarchy = new Hierarchy<GameEntity>("Test");
            var parent = nested ? new HierarchyNode<GameEntity>(new(parentId, "Parent"), null) : null;
            if (parent != null) hierarchy.AddNode(parent, true);
            var count = grouped ? 5 : 3;
            var initial = Enumerable.Range(0, count).ToArray();
            int[] expected = grouped ? disjoint ? [0, 3, 1, 4, 2] : [0, 3, 4, 1, 2] : [2, 0, 1];
            for (var i = 0; i < count; i++)
            {
                var id = entityApi.CreateNewEntity($"Sibling {i}")!.EntityId!.Value;
                entityApi.SetEntityParent(id, parentId, i);
                var editor = new InspectorTransformFixture(entityApi, id);
                editors.Add(editor);
                editor.Apply(entityApi.GetEntityData(id)!);
                var node = new HierarchyNode<GameEntity>(editor.Entity, parent);
                parent?.PushChild(node);
                hierarchy.AddNode(node, parent == null);
            }
            var scenes = new TestSceneManagementService(editors.Select(e => e.Entity).ToArray());
            var runner = editors[0].Runner;
            var registry = new TestBehaviourRegistry();
            var managementLogger = new TestLogger<EntityManagementService>();
            var management = new EntityManagementService(managementLogger, scenes, null!, entityApi, registry, runner,
                new TestEntitySyncService { OnUpdate = entity => editors.Single(e => e.Entity == entity).Apply(entityApi.GetEntityData(entity.Id)!) },
                new RectTransformLayoutService(registry, scenes, null!));
            using var drag = new HierarchyDragFixture(hierarchy, management);
            CheckOrders(initial);
            Assert.Empty(received);

            int[] selection = grouped ? disjoint ? [editors[3].Entity.Id, editors[1].Entity.Id] : [editors[2].Entity.Id, editors[1].Entity.Id] : [editors[0].Entity.Id];
            drag.ViewModel.MoveEntities(selection, nested ? parentId : null, count);
            CheckOrders(expected);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            await commands.UndoAsync();
            CheckOrders(initial);
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            // Dropping in the same slot must preserve Redo and produce no callback.
            int[] noOp = grouped ? [editors[1].Entity.Id, editors[2].Entity.Id] : [editors[0].Entity.Id];
            drag.ViewModel.MoveEntities(noOp, nested ? parentId : null, grouped ? 3 : 1);
            CheckOrders(initial);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            CheckOrders(expected);

            if (grouped)
            {
                entityApi.ReorderEntities([editors[0].Entity.Id, int.MaxValue], 0, recordHierarchyChange: true);
                entityApi.ReorderEntities([editors[0].Entity.Id, editors[0].Entity.Id], 0, recordHierarchyChange: true);
                if (nested) entityApi.ReorderEntities([editors[0].Entity.Id, parentId], 0, recordHierarchyChange: true);
                CheckOrders(expected);
                Dispatcher.UIThread.RunJobs();
                Assert.Single(received);
            }

            api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", 0, 1);
            Dispatcher.UIThread.RunJobs();
            var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(editors[0].Entity, "Renamed"));
            Assert.Equal("Renamed", entityApi.GetEntityData(editors[0].Entity.Id)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Sibling 0", entityApi.GetEntityData(editors[0].Entity.Id)!.Name);
            await commands.UndoAsync();
            CheckMove(1);
            CheckOrders(expected);
            await commands.UndoAsync();
            CheckOrders(initial);
            Assert.False(commands.CanUndo);
            await commands.RedoAsync();
            CheckOrders(expected);
            await commands.RedoAsync();
            CheckMove(4);
            await commands.RedoAsync();
            Assert.Equal("Renamed", entityApi.GetEntityData(editors[0].Entity.Id)!.Name);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanRedo);
            Assert.Equal(2, received.Count);

            Assert.Empty(logger.Entries);
            Assert.Empty(managementLogger.Entries);

            void CheckOrders(int[] expected)
            {
                for (var i = 0; i < editors.Count; i++)
                {
                    var state = entityApi.GetEntityData(editors[i].Entity.Id)!;
                    var transform = state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Transform");
                    Assert.Equal(expected[i], Convert.ToInt32(transform["_order"]));
                    Assert.Equal(parentId, Convert.ToInt32(transform["_parent"]));
                    editors[i].Apply(state);
                    Assert.Equal(expected[i], Convert.ToInt32(editors[i].Transform.GetProperty("_order").Value));
                    foreach (var name in new[] { "_position", "_scale", "_rotation" })
                    {
                        var vector = JObject.FromObject(transform[name]);
                        foreach (var axis in new[] { "x", "y", "z" })
                            Assert.InRange(MathF.Abs(vector[axis]!.Value<float>() - (name == "_scale" ? 1f : 0f)), 0f, 0.0001f);
                    }
                    Assert.Empty(editors[i].Logger.Entries);
                }
            }

            void CheckMove(float x)
            {
                var values = new float[10];
                api.Invoke(typeof(ReadTransformDelegate), "NativeProjectReadActionTransform", 0, (object)values);
                Assert.Equal(new[] { x, x + 1, x + 2 }, values.Take(3));
            }
        }
        finally
        {
            foreach (var editor in editors) editor.Dispose();
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task HierarchyReparentPreservesTransformsAndSharesHistoryWithInspectorAndRename()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var mode = 0; mode < 3; mode++) await VerifyHierarchyReparentAsync(mode);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task GroupParentChangesRecordOneEntryAndPreserveWorldTransforms()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var mode = 0; mode < 3; mode++)
                foreach (var disjoint in new[] { false, true }) await VerifyHierarchyReparentAsync(mode, grouped: true, disjoint: disjoint);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task MixedParentDragUsesHierarchyOrderAndOneHistoryEntry()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var mode = 0; mode < 4; mode++)
                foreach (var disjoint in new[] { false, true }) await VerifyHierarchyReparentAsync(mode, grouped: true, disjoint: disjoint, mixedParents: true);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyHierarchyReparentAsync(int mode, bool grouped = false, bool disjoint = false, bool mixedParents = false)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entityApi = new EntityApi(api);
        using var commands = new EditorCommandService();
        var historyLogger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, historyLogger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        var editors = new List<InspectorTransformFixture>();
        var sourceLogger = new TestLogger<EngineSceneSyncEventSource>();
        var source = new EngineSceneSyncEventSource(api, sourceLogger);
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            foreach (var name in new[] { "Parent A", "Parent B", "Target", "Source 1", "Source 2", "Destination 1", "Destination 2" })
                editors.Add(new(entityApi, entityApi.CreateNewEntity(name)!.EntityId!.Value));
            if (mixedParents && mode != 3) editors.Add(new(entityApi, entityApi.CreateNewEntity("Parent C")!.EntityId!.Value));
            var target = editors[2];
            var second = editors[disjoint ? 4 : 3];
            int[] movedIds = grouped ? [target.Entity.Id, second.Entity.Id] : [target.Entity.Id];
            var oldParent = mode == 2 ? 0 : editors[0].Entity.Id;
            var newParent = mode == 1 ? 0 : mixedParents && mode != 3 ? editors[7].Entity.Id : editors[1].Entity.Id;
            SetTransform(editors[0].Entity.Id, 10, 2, 30);
            SetTransform(editors[1].Entity.Id, -5, 3, -45);
            SetTransform(target.Entity.Id, 1, 1.5f, 15);
            if (grouped) SetTransform(second.Entity.Id, 7, 0.8f, -20);
            for (var i = 2; i < 7; i++)
                entityApi.SetEntityParent(editors[i].Entity.Id, i < 5 ? oldParent : newParent, i < 5 ? i - 2 : i - 5);
            if (mixedParents) entityApi.SetEntityParent(second.Entity.Id, editors[1].Entity.Id, 0);
            var scenes = new TestSceneManagementService(editors.Select(e => e.Entity).ToArray());
            var scene = scenes.Scene.Value!;
            scene.SetAssetInfo(new AssetInfo(new AssetMeta("empty-scene"), ""));
            var registry = new TestBehaviourRegistry();
            var managementLogger = new TestLogger<EntityManagementService>();
            var entitySync = new TestEntitySyncService { OnUpdate = entity => editors.Single(e => e.Entity == entity).Apply(entityApi.GetEntityData(entity.Id)!) };
            var management = new EntityManagementService(managementLogger, scenes, null!, entityApi, registry, target.Runner, entitySync,
                new RectTransformLayoutService(registry, scenes, null!));
            var syncLogger = new TestLogger<SceneSyncService>();
            using var sync = new SceneSyncService(source, scenes, new EngineSelectionSyncService(new SelectionService(new TestEntityApi())), syncLogger, entitySync);
            using var drag = new HierarchyDragFixture(scene.Hierarchy, management);
            source.Connect();
            var before = await ReadStateAsync();
            var world = ReadWorld();
            Assert.Empty(received);

            if (grouped)
            {
                entityApi.ReparentEntities([target.Entity.Id, int.MaxValue], newParent, 1, recordHierarchyChange: true);
                entityApi.ReparentEntities(movedIds, target.Entity.Id, 1, recordHierarchyChange: true);
                entityApi.ReparentEntities([target.Entity.Id, target.Entity.Id], newParent, 1, recordHierarchyChange: true);
                CheckState(before, await ReadStateAsync());
                Assert.Empty(received);
            }
            drag.ViewModel.MoveEntities(movedIds.Reverse().ToArray(), newParent == 0 ? null : newParent, 1);
            var after = await ReadStateAsync();
            Assert.Equal(newParent, after[target.Entity.Id]["_parent"]!.Value<int>());
            var expectedIndex = 1 - movedIds.Count(id => before[id]["_parent"]!.Value<int>() == newParent && before[id]["_order"]!.Value<int>() < 1);
            Assert.Equal(expectedIndex, after[target.Entity.Id]["_order"]!.Value<int>());
            if (grouped)
            {
                Assert.Equal(newParent, after[second.Entity.Id]["_parent"]!.Value<int>());
                Assert.Equal(expectedIndex + 1, after[second.Entity.Id]["_order"]!.Value<int>());
                if (mixedParents && mode == 3)
                    foreach (var name in new[] { "_position", "_scale", "_rotation" }) Assert.True(JToken.DeepEquals(before[second.Entity.Id][name], after[second.Entity.Id][name]));
            }
            CheckWorld(world);
            Assert.Single(received);

            using var vm = new Vector3PropertyViewModel(target.Transform.GetProperty("_position"));
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding("X") { Mode = BindingMode.TwoWay });
            input.TextInternal = (vm.X + 5).ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            var moved = await ReadStateAsync();
            Assert.Equal(2, received.Count);
            var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, target.Runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(target.Entity, "Renamed"));
            Assert.Equal("Renamed", entityApi.GetEntityData(target.Entity.Id)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Target", entityApi.GetEntityData(target.Entity.Id)!.Name);
            await commands.UndoAsync();
            CheckState(after, await ReadStateAsync());
            await commands.UndoAsync();
            CheckState(before, await ReadStateAsync());
            CheckWorld(world);
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            var originalIndex = scene.Hierarchy.GetNodeOrder(scene.Hierarchy.GetNode(target.Entity)!);
            int[] noOp = grouped && !mixedParents ? [target.Entity.Id, editors[3].Entity.Id] : [target.Entity.Id];
            drag.ViewModel.MoveEntities(noOp, oldParent == 0 ? null : oldParent, originalIndex + (grouped && !mixedParents ? 2 : 1));
            CheckState(before, await ReadStateAsync());
            Assert.True(commands.CanRedo);
            Assert.Equal(2, received.Count);
            await commands.RedoAsync();
            CheckState(after, await ReadStateAsync());
            CheckWorld(world);
            await commands.RedoAsync();
            CheckState(moved, await ReadStateAsync());
            await commands.RedoAsync();
            Assert.Equal("Renamed", entityApi.GetEntityData(target.Entity.Id)!.Name);
            Assert.False(commands.CanRedo);
            Assert.Equal(2, received.Count);

            // RectTransform parent changes remain outside history.
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectAddRectTransform", target.Entity.Id);
            drag.ViewModel.MoveEntities(movedIds, oldParent == 0 ? null : oldParent, 0);
            var rectState = await ReadStateAsync();
            Assert.Equal(grouped ? newParent : oldParent, rectState[target.Entity.Id]["_parent"]!.Value<int>());
            if (grouped) CheckState(moved, rectState);
            Assert.Equal(2, received.Count);
            Assert.Empty(historyLogger.Entries);
            Assert.Empty(managementLogger.Entries);
            Assert.Empty(sourceLogger.Entries);
            Assert.Empty(syncLogger.Entries);

            async Task<Dictionary<int, JObject>> ReadStateAsync()
            {
                var states = new Dictionary<int, JObject>();
                foreach (var editor in editors)
                {
                    var state = entityApi.GetEntityData(editor.Entity.Id)!;
                    states.Add(editor.Entity.Id, JObject.FromObject(state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Transform")));
                    editor.Apply(state);
                }
                source.RequestSnapshot();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline)
                {
                    Dispatcher.UIThread.RunJobs();
                    if (editors.All(e => e.Entity.Transform.Parent == states[e.Entity.Id]["_parent"]!.Value<int>() &&
                        e.Entity.Transform.Order == states[e.Entity.Id]["_order"]!.Value<int>())) break;
                    await Task.Delay(10);
                }
                foreach (var editor in editors)
                {
                    var state = states[editor.Entity.Id];
                    Assert.Equal(state["_parent"]!.Value<int>(), editor.Entity.Transform.Parent);
                    Assert.Equal(state["_order"]!.Value<int>(), editor.Entity.Transform.Order);
                    var node = scene.Hierarchy.GetNode(editor.Entity)!;
                    Assert.Equal(editor.Entity.Transform.Parent, node.Parent?.Content.Id ?? 0);
                    Assert.Equal(editor.Entity.Transform.Order, scene.Hierarchy.GetNodeOrder(node));
                    foreach (var name in new[] { "_position", "_scale", "_rotation" })
                    {
                        var values = (Dictionary<string, SerializedProperty>)editor.Transform.GetProperty(name).Value!;
                        foreach (var axis in new[] { "x", "y", "z" })
                            Assert.InRange(MathF.Abs(state[name]![axis]!.Value<float>() - Convert.ToSingle(values[axis].Value)), 0f, 0.0001f);
                    }
                    Assert.Empty(editor.Logger.Entries);
                }
                return states;
            }

            Dictionary<int, float[]> ReadWorld()
            {
                var result = new Dictionary<int, float[]>();
                foreach (var id in movedIds)
                {
                    var values = new float[10];
                    api.Invoke(typeof(ReadEntityWorldTransformDelegate), "NativeProjectReadEntityWorldTransform", id, (object)values);
                    result.Add(id, values);
                }
                return result;
            }

            void CheckWorld(Dictionary<int, float[]> expected)
            {
                var actual = ReadWorld();
                foreach (var (id, values) in expected)
                {
                    for (var i = 0; i < 6; i++) Assert.InRange(MathF.Abs(values[i] - actual[id][i]), 0f, 0.0001f);
                    var dot = Enumerable.Range(6, 4).Sum(i => values[i] * actual[id][i]);
                    Assert.InRange(MathF.Abs(MathF.Abs(dot) - 1), 0f, 0.0001f);
                }
            }
        }
        finally
        {
            foreach (var editor in editors) editor.Dispose();
            source.Disconnect();
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }

        void SetTransform(int id, float position, float scale, float rotation)
        {
            var editor = editors.Single(e => e.Entity.Id == id);
            var state = entityApi.GetEntityData(id)!;
            var transform = state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Transform");
            transform["_position"] = JObject.FromObject(new { x = position, y = 2f, z = 3f });
            transform["_scale"] = JObject.FromObject(new { x = scale, y = scale, z = scale });
            transform["_rotation"] = JObject.FromObject(new { x = 0f, y = 0f, z = rotation });
            editor.Apply(state);
            var values = new Dictionary<string, object?> { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = InspectorTransformFixture.TRANSFORM_ID };
            foreach (var name in new[] { "_position", "_scale", "_rotation" })
                values[name] = SerializedPropertyJsonConverter.SerializeRuntimeProperty(editor.Transform.GetProperty(name));
            entityApi.SetData(new SetEntityDataRequest { SceneId = id, Behaviours = [values] });
        }

        static void CheckState(Dictionary<int, JObject> expected, Dictionary<int, JObject> actual)
        {
            foreach (var (id, state) in expected)
            {
                foreach (var name in new[] { "_parent", "_order" }) Assert.Equal(state[name]!.Value<int>(), actual[id][name]!.Value<int>());
                foreach (var name in new[] { "_position", "_scale", "_rotation" })
                    foreach (var axis in new[] { "x", "y", "z" })
                        Assert.InRange(MathF.Abs(state[name]![axis]!.Value<float>() - actual[id][name]![axis]!.Value<float>()), 0f, 0.0001f);
            }
        }
    }

    [NativeCallbackFact]
    public Task InspectorRectPositionConfirmationsShareHistoryWithGizmoMoves()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            await VerifyInspectorRectPositionAsync(true);
            await VerifyInspectorRectPositionAsync(false);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorRectPositionAsync(bool horizontal)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[1];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 2, 1, ids);
            using var inspector = new InspectorTransformFixture(entities, ids[0]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            var layout = new RectTransformLayoutService(registry, new TestSceneManagementService(inspector.Entity), null!);
            using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
            var baseline = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(horizontal ? nameof(vm.HorizontalPrimaryValue) : nameof(vm.VerticalPrimaryValue)) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.TextInternal = "55";
            Check(20, 30);
            Assert.Empty(received);
            input.LoseFocus();
            var x = horizontal ? 55f : 20f;
            var y = horizontal ? 30f : 55f;
            Check(x, y);
            Assert.Single(received);

            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectMove", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            Check(x + 24, y + 16);
            Assert.Single(received);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(x + 24, y + 16);
            Assert.Equal(2, received.Count);
            await commands.UndoAsync();
            Check(x, y);
            await commands.UndoAsync();
            Check(20, 30);
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            foreach (var text in new[] { horizontal ? "20" : "30", "invalid", "NaN" })
            {
                input.TextInternal = text;
                input.LoseFocus();
                Check(20, 30);
                Assert.Equal(2, received.Count);
                Assert.True(commands.CanRedo);
            }
            await commands.RedoAsync();
            Check(x, y);
            await commands.RedoAsync();
            Check(x + 24, y + 16);
            Assert.False(commands.CanRedo);
            Assert.Equal(2, received.Count);
            await commands.UndoAsync();
            Check(x, y);
            input.TextInternal = "75";
            input.LoseFocus();
            Check(horizontal ? 75 : x, horizontal ? y : 75);
            Assert.Equal(3, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(x, y);
            Assert.Equal(3, received.Count);
            Assert.Empty(logger.Entries);
            // Partial anchor payloads stay unrecorded; explicit position edits support stretch.
            var excluded = new SetEntityDataRequest
            {
                SceneId = ids[0], RecordTransformChange = true,
                Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = component.Id,
                    ["_anchorMax"] = layout.SerializeVector2(new(1, 0.5f)),
                    ["_anchoredPosition"] = layout.SerializeVector2(new(88, 99)) }]
            };
            entities.SetData(excluded);
            var unsupported = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
            Assert.Equal(88f, unsupported[0]);
            Assert.Equal(99f, unsupported[1]);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, received.Count);
            excluded.Behaviours[0].Remove("_anchorMax");
            excluded.Behaviours[0]["_anchoredPosition"] = layout.SerializeVector2(new(90, 100));
            entities.SetData(excluded);
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
            Assert.Equal(90f, unsupported[0]);
            Assert.Equal(100f, unsupported[1]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.IsStretchX);
            Assert.Equal(4, received.Count);
            foreach (var undo in new[] { true, false })
            {
                if (undo) await commands.UndoAsync();
                else await commands.RedoAsync();
                var expected = undo ? new RectTransformVector2(88, 99) : new RectTransformVector2(90, 100);
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
                Assert.Equal(expected.X, unsupported[0]);
                Assert.Equal(expected.Y, unsupported[1]);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.True(layout.TryReadLayout(component, out var state));
                Assert.Equal(expected, state.AnchoredPosition);
                Assert.Equal(new RectTransformVector2(1, 0.5f), state.AnchorMax);
                Assert.Equal(1f, unsupported[6]);
                Assert.Equal(0.5f, unsupported[7]);
                Assert.Equal(4, received.Count);
            }
            Assert.Empty(inspector.Logger.Entries);

            void Check(float expectedX, float expectedY)
            {
                var native = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.InRange(MathF.Abs(native[0] - expectedX), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(native[1] - expectedY), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(vm.HorizontalPrimaryValue - expectedX), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(vm.VerticalPrimaryValue - expectedY), 0f, 0.0001f);
                for (var i = 2; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task InspectorRectSizeConfirmationsShareHistoryWithHandlesAndPosition()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            await VerifyInspectorRectSizeAsync(true);
            await VerifyInspectorRectSizeAsync(false);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorRectSizeAsync(bool horizontal)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[1];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, 1, ids);
            using var inspector = new InspectorTransformFixture(entities, ids[0]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            var layout = new RectTransformLayoutService(registry, new TestSceneManagementService(inspector.Entity), null!);
            using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
            var baseline = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var sizeBinding = input.Bind(ReiTextBox.TextProperty, new Binding(horizontal ? nameof(vm.HorizontalSecondaryValue) : nameof(vm.VerticalSecondaryValue)) { Mode = BindingMode.TwoWay });
            var positionInput = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var positionBinding = positionInput.Bind(ReiTextBox.TextProperty, new Binding(horizontal ? nameof(vm.HorizontalPrimaryValue) : nameof(vm.VerticalPrimaryValue)) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "2";
            input.TextInternal = "255";
            Check(20, 30, 200, 160);
            Assert.Empty(received);
            input.LoseFocus();
            var width = horizontal ? 255f : 200f;
            var height = horizontal ? 160f : 255f;
            Check(20, 30, width, height);
            Assert.Single(received);
            positionInput.TextInternal = "11";
            positionInput.LoseFocus();
            var x = horizontal ? 11f : 20f;
            var y = horizontal ? 30f : 11f;
            Check(x, y, width, height);
            Assert.Equal(2, received.Count);
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            Check(x + 12, y + 8, width + 24, height + 16);
            Assert.Equal(2, received.Count);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(x + 12, y + 8, width + 24, height + 16);
            Assert.Equal(3, received.Count);
            await commands.UndoAsync();
            Check(x, y, width, height);
            await commands.UndoAsync();
            Check(20, 30, width, height);
            await commands.UndoAsync();
            Check(20, 30, 200, 160);
            Assert.False(commands.CanUndo);
            foreach (var text in new[] { horizontal ? "200" : "160", "invalid", "NaN" })
            {
                input.TextInternal = text;
                input.LoseFocus();
                Check(20, 30, 200, 160);
                Assert.Equal(3, received.Count);
                Assert.True(commands.CanRedo);
            }
            await commands.RedoAsync();
            Check(20, 30, width, height);
            await commands.RedoAsync();
            Check(x, y, width, height);
            await commands.RedoAsync();
            Check(x + 12, y + 8, width + 24, height + 16);
            Assert.False(commands.CanRedo);
            Assert.Equal(3, received.Count);
            await commands.UndoAsync();
            Check(x, y, width, height);
            input.TextInternal = "75";
            input.LoseFocus();
            Check(x, y, horizontal ? 75 : width, horizontal ? height : 75);
            Assert.Equal(4, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(x, y, width, height);
            await commands.RedoAsync();
            Check(x, y, horizontal ? 75 : width, horizontal ? height : 75);
            Assert.Equal(4, received.Count);
            // Partial anchor payloads stay unrecorded; explicit size edits support stretch.
            var excluded = new SetEntityDataRequest
            {
                SceneId = ids[0], RecordTransformChange = true,
                Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = component.Id,
                    ["_anchorMax"] = layout.SerializeVector2(new(1, 0.5f)),
                    ["_sizeDelta"] = layout.SerializeVector2(new(88, 99)) }]
            };
            entities.SetData(excluded);
            var unsupported = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
            Assert.Equal(88f, unsupported[2]);
            Assert.Equal(99f, unsupported[3]);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, received.Count);
            excluded.Behaviours[0].Remove("_anchorMax");
            excluded.Behaviours[0]["_sizeDelta"] = layout.SerializeVector2(new(90, 100));
            entities.SetData(excluded);
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
            Assert.Equal(90f, unsupported[2]);
            Assert.Equal(100f, unsupported[3]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.IsStretchX);
            Assert.Equal(5, received.Count);
            foreach (var undo in new[] { true, false })
            {
                if (undo) await commands.UndoAsync();
                else await commands.RedoAsync();
                var expected = undo ? new RectTransformVector2(88, 99) : new RectTransformVector2(90, 100);
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, unsupported);
                Assert.Equal(expected.X, unsupported[2]);
                Assert.Equal(expected.Y, unsupported[3]);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.True(layout.TryReadLayout(component, out var state));
                Assert.Equal(expected, state.SizeDelta);
                Assert.Equal(new RectTransformVector2(1, 0.5f), state.AnchorMax);
                Assert.Equal(1f, unsupported[6]);
                Assert.Equal(0.5f, unsupported[7]);
                Assert.Equal(5, received.Count);
            }
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void Check(float expectedX, float expectedY, float expectedWidth, float expectedHeight)
            {
                var native = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                var runtime = new[] { native[0], native[1], native[2], native[3] };
                var editor = new[] { vm.HorizontalPrimaryValue, vm.VerticalPrimaryValue, vm.HorizontalSecondaryValue, vm.VerticalSecondaryValue };
                var expected = new[] { expectedX, expectedY, expectedWidth, expectedHeight };
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.InRange(MathF.Abs(runtime[i] - expected[i]), 0f, 0.0001f);
                    Assert.InRange(MathF.Abs(editor[i] - expected[i]), 0f, 0.0001f);
                }
                for (var i = 4; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task InspectorRectOffsetsShareHistoryWithHandlesAndPreserveOppositeEdges()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var edge = 0; edge < 4; edge++) await VerifyInspectorRectOffsetAsync(edge);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorRectOffsetAsync(int edge)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[1];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, 1, ids);
            using var inspector = new InspectorTransformFixture(entities, ids[0]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            var layout = new RectTransformLayoutService(registry, new TestSceneManagementService(inspector.Entity), null!);
            entities.SetData(new SetEntityDataRequest
            {
                SceneId = ids[0], Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = component.Id,
                    ["_anchorMin"] = layout.SerializeVector2(new(0, 0)), ["_anchorMax"] = layout.SerializeVector2(new(1, 1)),
                    ["_sizeDelta"] = layout.SerializeVector2(new(-200, -160)) }]
            });
            var baseline = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
            var properties = new[] { nameof(vm.HorizontalPrimaryValue), nameof(vm.HorizontalSecondaryValue), nameof(vm.VerticalPrimaryValue), nameof(vm.VerticalSecondaryValue) };
            var initialOffsets = new[] { 120f, 80f, 50f, 110f };
            var changed = new[] { new[] { 28f, 30f, -216f, -160f }, new[] { 12f, 30f, -216f, -160f },
                new[] { 20f, 22f, -200f, -176f }, new[] { 20f, 38f, -200f, -176f } }[edge];
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(properties[edge]) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.TextInternal = (initialOffsets[edge] + 16).ToString(System.Globalization.CultureInfo.CurrentCulture);
            Check(baseline);
            Assert.Empty(received);
            input.LoseFocus();
            Check(changed);
            var offsets = new[] { vm.HorizontalPrimaryValue, vm.HorizontalSecondaryValue, vm.VerticalPrimaryValue, vm.VerticalSecondaryValue };
            for (var i = 0; i < 4; i++) Assert.Equal(initialOffsets[i] + (i == edge ? 16 : 0), offsets[i]);
            Assert.Single(received);
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            var dragged = new[] { changed[0] + 12, changed[1] + 8, changed[2] + 24, changed[3] + 16 };
            Check(dragged);
            Assert.Single(received);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(dragged);
            Assert.Equal(2, received.Count);
            await commands.UndoAsync();
            Check(changed);
            await commands.UndoAsync();
            Check(baseline);
            Assert.False(commands.CanUndo);
            foreach (var text in new[] { initialOffsets[edge].ToString(System.Globalization.CultureInfo.CurrentCulture), "invalid", "NaN" })
            {
                input.TextInternal = text;
                input.LoseFocus();
                Check(baseline);
                Assert.Equal(2, received.Count);
                Assert.True(commands.CanRedo);
            }
            await commands.RedoAsync();
            Check(changed);
            await commands.RedoAsync();
            Check(dragged);
            Assert.Equal(2, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(changed);
            input.TextInternal = (initialOffsets[edge] + 32).ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            var branched = changed.Select((value, index) => baseline[index] + 2 * (value - baseline[index])).ToArray();
            Check(branched);
            Assert.Equal(3, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(changed);
            await commands.RedoAsync();
            Check(branched);
            Assert.Equal(3, received.Count);
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void Check(float[] expected)
            {
                var native = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.True(layout.TryReadLayout(component, out var state));
                var editor = new[] { state.AnchoredPosition.X, state.AnchoredPosition.Y, state.SizeDelta.X, state.SizeDelta.Y };
                for (var i = 0; i < 4; i++)
                {
                    Assert.InRange(MathF.Abs(native[i] - expected[i]), 0f, 0.0001f);
                    Assert.InRange(MathF.Abs(editor[i] - expected[i]), 0f, 0.0001f);
                }
                // With full stretch and a centered pivot, offsets follow directly from native position and size delta.
                var nativeEdges = new[] { native[0] - native[2] / 2, -native[0] - native[2] / 2, -native[1] - native[3] / 2, native[1] - native[3] / 2 };
                var editorEdges = new[] { vm.HorizontalPrimaryValue, vm.HorizontalSecondaryValue, vm.VerticalPrimaryValue, vm.VerticalSecondaryValue };
                for (var i = 0; i < 4; i++) Assert.InRange(MathF.Abs(editorEdges[i] - nativeEdges[i]), 0f, 0.0001f);
                for (var i = 4; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task InspectorRectPivotPreservesBoundsAndSharesHistoryWithHandles()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var stretch in new[] { false, true })
            {
                await VerifyInspectorRectPivotAsync(true, stretch);
                await VerifyInspectorRectPivotAsync(false, stretch);
            }
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorRectPivotAsync(bool horizontal, bool stretch)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[1];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, 1, ids);
            using var inspector = new InspectorTransformFixture(entities, ids[0]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            var layout = new RectTransformLayoutService(registry, new TestSceneManagementService(inspector.Entity), null!);
            if (stretch)
            {
                entities.SetData(new SetEntityDataRequest
                {
                    SceneId = ids[0], Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = component.Id,
                        ["_anchorMin"] = layout.SerializeVector2(new(0, 0)), ["_anchorMax"] = layout.SerializeVector2(new(1, 1)),
                        ["_sizeDelta"] = layout.SerializeVector2(new(-200, -160)) }]
                });
            }
            var baseline = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
            var initial = new[] { baseline[0], baseline[1], baseline[2], baseline[3], baseline[8], baseline[9] };
            var bounds = Bounds(baseline);
            var changed = new[] { baseline[0] - (horizontal ? baseline[2] / 4 : 0), baseline[1] - (horizontal ? 0 : baseline[3] / 4),
                baseline[2], baseline[3], horizontal ? 0.25f : 0.5f, horizontal ? 0.5f : 0.25f };
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(horizontal ? nameof(vm.PivotX) : nameof(vm.PivotY)) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "0";
            input.TextInternal = 0.25f.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Check(initial, bounds);
            Assert.Empty(received);
            input.LoseFocus();
            Check(changed, bounds);
            Assert.Single(received);
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            var dragged = new[] { changed[0] + 24 * changed[4], changed[1] + 16 * changed[5], changed[2] + 24, changed[3] + 16, changed[4], changed[5] };
            var draggedBounds = new[] { bounds[0], bounds[1], bounds[2] + 24, bounds[3] + 16 };
            Check(dragged, draggedBounds);
            Assert.Single(received);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(dragged, draggedBounds);
            Assert.Equal(2, received.Count);
            await commands.UndoAsync();
            Check(changed, bounds);
            await commands.UndoAsync();
            Check(initial, bounds);
            Assert.False(commands.CanUndo);
            foreach (var text in new[] { 0.5f.ToString(System.Globalization.CultureInfo.CurrentCulture), "invalid", "NaN" })
            {
                input.TextInternal = text;
                input.LoseFocus();
                Check(initial, bounds);
                Assert.Equal(2, received.Count);
                Assert.True(commands.CanRedo);
            }
            await commands.RedoAsync();
            Check(changed, bounds);
            await commands.RedoAsync();
            Check(dragged, draggedBounds);
            Assert.Equal(2, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(changed, bounds);
            input.TextInternal = 0.75f.ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            var branched = new[] { baseline[0] + (horizontal ? baseline[2] / 4 : 0), baseline[1] + (horizontal ? 0 : baseline[3] / 4),
                baseline[2], baseline[3], horizontal ? 0.75f : 0.5f, horizontal ? 0.5f : 0.75f };
            Check(branched, bounds);
            Assert.Equal(3, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(changed, bounds);
            await commands.RedoAsync();
            Check(branched, bounds);
            Assert.Equal(3, received.Count);
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void Check(float[] expected, float[] expectedBounds)
            {
                var native = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.True(layout.TryReadLayout(component, out var state));
                var editor = new[] { state.AnchoredPosition.X, state.AnchoredPosition.Y, state.SizeDelta.X, state.SizeDelta.Y, vm.PivotX, vm.PivotY };
                var actual = new[] { native[0], native[1], native[2], native[3], native[8], native[9] };
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.InRange(MathF.Abs(actual[i] - expected[i]), 0f, 0.0001f);
                    Assert.InRange(MathF.Abs(editor[i] - expected[i]), 0f, 0.0001f);
                }
                var actualBounds = Bounds(native);
                for (var i = 0; i < 4; i++)
                {
                    Assert.InRange(MathF.Abs(actualBounds[i] - expectedBounds[i]), 0f, 0.0001f);
                    Assert.Equal(baseline[4 + i], native[4 + i]);
                }
                for (var i = 10; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
            }

            static float[] Bounds(float[] native)
            {
                // Shared real-engine fixture uses a 640x480 constant-pixel-size Canvas and identity Transform.
                var left = 640 * native[4] + native[0] - native[8] * native[2];
                var bottom = 480 * native[5] + native[1] - native[9] * native[3];
                return [left, bottom, left + 640 * (native[6] - native[4]) + native[2], bottom + 480 * (native[7] - native[5]) + native[3]];
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task InspectorRectPresetsPreserveBoundsAndShareHistoryWithHandles()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
            var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
            Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
            var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
            using var session = factory.CreateIsolatedSession(path!);
            var api = session.EngineApi;
            var entities = new EntityApi(api);
            using var commands = new EditorCommandService();
            var logger = new TestLogger<NativeEditorHistoryRecorder>();
            using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
            var received = new List<ulong>();
            api.ActionRecorded += received.Add;
            CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
            try
            {
                api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
                api.MarkEngineStarted();
                var ids = new int[1];
                api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, 1, ids);
                using var inspector = new InspectorTransformFixture(entities, ids[0]);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
                var registry = new TestBehaviourRegistry();
                registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
                var parentId = Convert.ToInt32(inspector.Transform.GetProperty("_parent").Value);
                Assert.NotEqual(0, parentId);
                // Hydrate the Editor parent with the effective size of the shared fixture's native Canvas.
                var parent = new GameEntity(parentId, "Canvas bounds");
                parent.AddBehaviour(SceneComponentBuilder.RectTransform(InspectorTransformFixture.RECT_TRANSFORM_ID,
                    new(new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(0, 0), new(640, 480))));
                var scenes = new TestSceneManagementService(parent, inspector.Entity);
                Assert.True(scenes.Scene.Value!.MoveEntity(inspector.Entity, parent, 0));
                var layout = new RectTransformLayoutService(registry, scenes, null!);
                Assert.Equal(new RectTransformVector2(640, 480), layout.GetParentSize(inspector.Entity));
                using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
                var baseline = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
                var initial = new[] { 20f, 30f, 200f, 160f, 0.5f, 0.5f, 0.5f, 0.5f };
                var stretched = new[] { 20f, 30f, -440f, -320f, 0f, 0f, 1f, 1f };
                var fixedTopLeft = new[] { 340f, -210f, 200f, 160f, 0f, 1f, 0f, 1f };
                var dragged = new[] { 352f, -202f, 224f, 176f, 0f, 1f, 0f, 1f };
                var bounds = new[] { 240f, 190f, 440f, 350f };
                var draggedBounds = new[] { 240f, 190f, 464f, 366f };
                var center = Assert.Single(vm.Presets, preset => preset.DisplayName == "Middle Center");
                var stretch = Assert.Single(vm.Presets, preset => preset.DisplayName == "Stretch Both");
                var topLeft = Assert.Single(vm.Presets, preset => preset.DisplayName == "Top Left");
                Check(initial, bounds, center);
                center.ApplyCommand.Execute(null);
                Check(initial, bounds, center);
                Assert.Empty(received);
                stretch.ApplyCommand.Execute(null);
                Check(stretched, bounds, stretch);
                Assert.Single(received);
                topLeft.ApplyCommand.Execute(null);
                Check(fixedTopLeft, bounds, topLeft);
                Assert.Equal(2, received.Count);
                api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
                api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
                Check(dragged, draggedBounds, topLeft);
                Assert.Equal(2, received.Count);
                api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
                Check(dragged, draggedBounds, topLeft);
                Assert.Equal(3, received.Count);
                await commands.UndoAsync();
                Check(fixedTopLeft, bounds, topLeft);
                await commands.UndoAsync();
                Check(stretched, bounds, stretch);
                await commands.UndoAsync();
                Check(initial, bounds, center);
                Assert.False(commands.CanUndo);
                center.ApplyCommand.Execute(null);
                Check(initial, bounds, center);
                Assert.Equal(3, received.Count);
                Assert.True(commands.CanRedo);
                await commands.RedoAsync();
                Check(stretched, bounds, stretch);
                await commands.RedoAsync();
                Check(fixedTopLeft, bounds, topLeft);
                await commands.RedoAsync();
                Check(dragged, draggedBounds, topLeft);
                Assert.Equal(3, received.Count);
                Assert.False(commands.CanRedo);
                await commands.UndoAsync();
                Check(fixedTopLeft, bounds, topLeft);
                center.ApplyCommand.Execute(null);
                Check(initial, bounds, center);
                Assert.Equal(4, received.Count);
                Assert.False(commands.CanRedo);
                await commands.UndoAsync();
                Check(fixedTopLeft, bounds, topLeft);
                await commands.RedoAsync();
                Check(initial, bounds, center);
                Assert.Equal(4, received.Count);
                // Partial anchor requests without a complete rectangle snapshot remain unrecorded.
                entities.SetData(new SetEntityDataRequest
                {
                    SceneId = ids[0], RecordTransformChange = true,
                    Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = component.Id,
                        ["_anchorMin"] = layout.SerializeVector2(new(0.25f, 0.25f)),
                        ["_anchorMax"] = layout.SerializeVector2(new(0.75f, 0.75f)) }]
                });
                var manual = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, manual);
                Assert.Equal(0.25f, manual[4]);
                Assert.Equal(0.75f, manual[6]);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(4, received.Count);
                Assert.Empty(logger.Entries);
                Assert.Empty(inspector.Logger.Entries);

                void Check(float[] expected, float[] expectedBounds, RectTransformAnchorPresetViewModel selected)
                {
                    var native = new float[20];
                    api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                    inspector.Apply(entities.GetEntityData(ids[0])!);
                    Dispatcher.UIThread.RunJobs();
                    Assert.True(layout.TryReadLayout(component, out var state));
                    var editor = new[] { state.AnchoredPosition.X, state.AnchoredPosition.Y, state.SizeDelta.X, state.SizeDelta.Y,
                        vm.AnchorMinX, vm.AnchorMinY, vm.AnchorMaxX, vm.AnchorMaxY };
                    for (var i = 0; i < 8; i++)
                    {
                        Assert.InRange(MathF.Abs(native[i] - expected[i]), 0f, 0.0001f);
                        Assert.InRange(MathF.Abs(editor[i] - expected[i]), 0f, 0.0001f);
                    }
                    var left = 640 * native[4] + native[0] - native[8] * native[2];
                    var bottom = 480 * native[5] + native[1] - native[9] * native[3];
                    var nativeBounds = new[] { left, bottom, left + 640 * (native[6] - native[4]) + native[2], bottom + 480 * (native[7] - native[5]) + native[3] };
                    for (var i = 0; i < 4; i++) Assert.InRange(MathF.Abs(nativeBounds[i] - expectedBounds[i]), 0f, 0.0001f);
                    for (var i = 8; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
                    Assert.Same(selected, Assert.Single(vm.Presets, preset => preset.IsSelected));
                }
            }
            finally
            {
                api.MarkEngineStopped();
                api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
                GC.KeepAlive(createEngine);
            }
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformReparentSharesHistoryWithInspectorAndHandles()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var stretch in new[] { false, true })
                foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformGroupReparentKeepsHierarchyOrderAndSharesOneHistoryEntry()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var stretch in new[] { false, true })
                foreach (var toCanvas in new[] { false, true })
                    foreach (var disjoint in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas, grouped: true, disjoint: disjoint);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformMixedParentDragKeepsHierarchyOrderAndSharesOneHistoryEntry()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var stretch in new[] { false, true })
                foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas, grouped: true, mixedParents: true);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformCrossCanvasReparentPreservesScreenBoundsAndSharesHistory()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var scale in new[] { 1f, 2f })
                foreach (var stretch in new[] { false, true })
                    foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas, canvasScales: (scale, scale));
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformCrossCanvasGroupReparentPreservesScreenBoundsAndSharesOneEntry()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var scale in new[] { 1f, 2f })
                foreach (var stretch in new[] { false, true })
                    foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas, grouped: true, canvasScales: (scale, scale));
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformCrossCanvasMixedParentDragPreservesScreenBoundsAndHierarchyOrder()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var scale in new[] { 1f, 2f })
                foreach (var stretch in new[] { false, true })
                    foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(stretch ? "Stretch Both" : null, toCanvas, grouped: true, mixedParents: true, canvasScales: (scale, scale));
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformFixedAnchorsReparentAcrossDifferentCanvasScalesPreservesPixelsAndHistory()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var scales in new[] { (1f, 2f), (2f, 1f) })
                foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(null, toCanvas, canvasScales: scales);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackTheory]
    [InlineData("Middle Stretch X", false, false)]
    [InlineData("Center Stretch Y", false, false)]
    [InlineData("Stretch Both", false, false)]
    [InlineData(null, true, false)]
    [InlineData("Stretch Both", true, false)]
    [InlineData("Stretch Both", true, true)]
    public Task RectTransformReparentAcrossAllCanvasScales(string? preset, bool grouped, bool mixedParents)
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var scales in new[] { (1f, 2f), (2f, 1f) })
                foreach (var toCanvas in new[] { false, true }) await VerifyRectReparentAsync(preset, toCanvas, grouped: grouped, mixedParents: mixedParents, canvasScales: scales);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyRectReparentAsync(string? preset, bool toCanvas, bool grouped = false, bool disjoint = false, bool mixedParents = false, (float Source, float Destination)? canvasScales = null)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        var editors = new List<InspectorTransformFixture>();
        var source = new EngineSceneSyncEventSource(api, new TestLogger<EngineSceneSyncEventSource>());
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[grouped ? 6 : 5];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, ids.Length, ids);
            var canvasIds = new int[2];
            var sourceScale = canvasScales?.Source ?? 1f;
            var destinationScale = canvasScales?.Destination ?? 1f;
            if (canvasScales.HasValue)
            {
                api.Invoke(typeof(PrepareRectCanvasesDelegate), "NativeProjectPrepareRectCanvases", sourceScale, destinationScale, canvasIds);
                entities.SetEntityParent(ids[2], canvasIds[1], 0);
            }
            foreach (var id in ids)
            {
                var editor = new InspectorTransformFixture(entities, id);
                editor.Apply(entities.GetEntityData(id)!);
                editors.Add(editor);
            }
            var target = editors[0];
            var canvasId = Convert.ToInt32(target.Transform.GetProperty("_parent").Value);
            var canvas = CreateCanvasBounds(canvasId, "Source Canvas", sourceScale);
            var destinationCanvas = canvasScales.HasValue ? CreateCanvasBounds(canvasIds[1], "Destination Canvas", destinationScale) : canvas;
            var sceneEntities = editors.Select(e => e.Entity).Prepend(canvas);
            if (destinationCanvas != canvas) sceneEntities = sceneEntities.Append(destinationCanvas);
            var scenes = new TestSceneManagementService(sceneEntities.ToArray());
            var scene = scenes.Scene.Value!;
            scene.SetAssetInfo(new AssetInfo(new AssetMeta("render-empty-scene"), ""));
            foreach (var editor in editors) Assert.True(scene.MoveEntity(editor.Entity, canvas, int.MaxValue));
            if (canvasScales.HasValue) Assert.True(scene.MoveEntity(editors[2].Entity, destinationCanvas, 0));
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            registry.Ids[EngineBehavioursConstants.CANVAS] = 7302;
            var window = new TestEngineWindowController();
            window.Size.Value = (640, 480);
            var layout = new RectTransformLayoutService(registry, scenes, window);
            var managementLogger = new TestLogger<EntityManagementService>();
            var entitySync = new TestEntitySyncService { OnUpdate = entity => editors.Single(e => e.Entity == entity).Apply(entities.GetEntityData(entity.Id)!) };
            var management = new EntityManagementService(managementLogger, scenes, null!, entities, registry, target.Runner, entitySync, layout);
            using var sync = new SceneSyncService(source, scenes, new EngineSelectionSyncService(new SelectionService(new TestEntityApi())),
                new TestLogger<SceneSyncService>(), entitySync);
            using var drag = new HierarchyDragFixture(scene.Hierarchy, management);
            source.Connect();
            var component = target.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            using var vm = new RectTransformPropertyViewModel(target.Entity, component, target.Runner, entities, layout);
            if (preset != null) vm.Presets.Single(p => p.DisplayName == preset).ApplyCommand.Execute(null);
            // Prepare the hierarchy without recording the setup moves.
            management.SetParent(editors[3].Entity, editors[1].Entity, 0);
            management.SetParent(editors[4].Entity, editors[2].Entity, 0);
            management.SetParent(target.Entity, editors[1].Entity, 0);
            if (grouped) management.SetParent(editors[5].Entity, editors[mixedParents ? 2 : 1].Entity, mixedParents ? 0 : disjoint ? 2 : 1);
            entities.SelectEntity(target.Entity.Id);
            var before = await ReadAsync();
            var initialCount = received.Count;
            var destination = toCanvas ? destinationCanvas : editors[2].Entity;
            var bounds = PixelBounds(before, 0);
            var expectedPixels = sourceScale == 1f ? new[] { 240f, 190f, 440f, 350f } : new[] { 160f, 140f, 560f, 460f };
            CheckFloats(expectedPixels, bounds);
            var screenBefore = canvasScales.HasValue ? ReadScreenBounds() : null;
            if (screenBefore != null) CheckFloats(expectedPixels, screenBefore);
            var secondScreenBefore = canvasScales.HasValue && grouped ? ReadScreenBounds(5) : null;
            int[] movedIds = grouped ? [ids[5], ids[0]] : [ids[0]];
            if (grouped)
            {
                Assert.True(layout.TryPreserveRectForParent(target.Entity, destination, out var preserved));
                Assert.Throws<ArgumentException>(() => entities.ReparentEntities(movedIds, destination.Id, 1, true, [preserved]));
                entities.ReparentEntities([ids[0], int.MaxValue], destination.Id, 1, true, [preserved, preserved]);
                entities.ReparentEntities([ids[0], ids[0]], destination.Id, 1, true, [preserved, preserved]);
                entities.ReparentEntities(movedIds, ids[0], 1, true, [preserved, preserved]);
                entities.ReparentEntities([ids[0], canvasId], destination.Id, 1, true, [preserved, preserved]);
                entities.ReparentEntities(movedIds, destination.Id, 1, true);
                Check(before, await ReadAsync());
                Assert.Equal(initialCount, received.Count);
            }
            drag.ViewModel.MoveEntities(movedIds, destination.Id, 1);
            var after = await ReadAsync();
            var expectedIndex = 1 - movedIds.Count(id => before.Parents[Array.IndexOf(ids, id)] == destination.Id && before.Orders[Array.IndexOf(ids, id)] < 1);
            Assert.Equal(destination.Id, after.Parents[0]);
            Assert.Equal(expectedIndex, after.Orders[0]);
            CheckFloats(bounds, PixelBounds(after, 0));
            if (screenBefore != null) CheckFloats(screenBefore, ReadScreenBounds());
            if (grouped)
            {
                Assert.Equal(destination.Id, after.Parents[5]);
                Assert.Equal(expectedIndex + 1, after.Orders[5]);
                CheckFloats(PixelBounds(before, 5), PixelBounds(after, 5));
                if (secondScreenBefore != null) CheckFloats(secondScreenBefore, ReadScreenBounds(5));
                drag.ViewModel.MoveEntities(movedIds, destination.Id, expectedIndex + 2);
                Check(after, await ReadAsync());
            }
            Assert.Equal(initialCount + 1, received.Count);
            // Inspector and real pointer handles append entries to the same stream.
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(nameof(vm.HorizontalSecondaryValue)) { Mode = BindingMode.TwoWay });
            input.TextInternal = (vm.HorizontalSecondaryValue + (vm.IsStretchX ? -10 : 20)).ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            var resized = await ReadAsync();
            Assert.Equal(initialCount + 2, received.Count);
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            var handled = await ReadAsync();
            Assert.Equal(initialCount + 3, received.Count);
            await commands.UndoAsync();
            Check(resized, await ReadAsync());
            await commands.UndoAsync();
            Check(after, await ReadAsync());
            await commands.UndoAsync();
            Check(before, await ReadAsync());
            var groupNoOp = grouped && !disjoint && !mixedParents;
            drag.ViewModel.MoveEntities(groupNoOp ? movedIds : [ids[0]], editors[1].Entity.Id, groupNoOp ? 2 : 1);
            Check(before, await ReadAsync());
            Assert.Equal(initialCount + 3, received.Count);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            Check(after, await ReadAsync());
            await commands.RedoAsync();
            Check(resized, await ReadAsync());
            await commands.RedoAsync();
            Check(handled, await ReadAsync());
            Assert.False(commands.CanRedo);
            Assert.Equal(initialCount + 3, received.Count);
            Assert.Empty(logger.Entries);
            Assert.Empty(managementLogger.Entries);

            async Task<(float[][] Rects, int[] Parents, int[] Orders)> ReadAsync()
            {
                var rects = new float[ids.Length][];
                var parents = new int[ids.Length];
                var orders = new int[ids.Length];
                for (var i = 0; i < ids.Length; i++)
                {
                    rects[i] = new float[20];
                    api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", i, rects[i]);
                    var state = entities.GetEntityData(ids[i])!;
                    var transform = JObject.FromObject(state.Behaviours.Single(b => b["REI_TYPE"].ToString() == "Transform"));
                    parents[i] = transform["_parent"]!.Value<int>();
                    orders[i] = transform["_order"]!.Value<int>();
                    editors[i].Apply(state);
                }
                source.RequestSnapshot();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (editors.Where((e, i) => e.Entity.Transform.Parent != parents[i] || e.Entity.Transform.Order != orders[i]).Any())
                {
                    Assert.True(DateTime.UtcNow < deadline, "SceneSync did not apply the native hierarchy.");
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }
                Dispatcher.UIThread.RunJobs();
                for (var i = 0; i < ids.Length; i++)
                {
                    var node = scene.Hierarchy.GetNode(editors[i].Entity)!;
                    Assert.Equal(parents[i], node.Parent?.Content.Id ?? 0);
                    Assert.Equal(orders[i], scene.Hierarchy.GetNodeOrder(node));
                    Assert.True(layout.TryReadLayout(editors[i].Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!, out var value));
                    CheckFloats(rects[i].Take(10).ToArray(), [value.AnchoredPosition.X, value.AnchoredPosition.Y, value.SizeDelta.X, value.SizeDelta.Y,
                        value.AnchorMin.X, value.AnchorMin.Y, value.AnchorMax.X, value.AnchorMax.Y, value.Pivot.X, value.Pivot.Y]);
                    Assert.Empty(editors[i].Logger.Entries);
                }
                if (canvasScales.HasValue)
                {
                    for (var i = 0; i < ids.Length; i++)
                        CheckFloats(PixelBounds((rects, parents, orders), i), ReadScreenBounds(i));
                }
                return (rects, parents, orders);
            }

            GameEntity CreateCanvasBounds(int id, string name, float scale)
            {
                var result = new GameEntity(id, name);
                result.AddBehaviour(SceneComponentBuilder.RectTransform(InspectorTransformFixture.RECT_TRANSFORM_ID,
                    new(new(0, 0), new(0, 0), new(0, 0), new(0, 0), new(640 / scale, 480 / scale))));
                if (canvasScales.HasValue)
                {
                    var data = JObject.FromObject(entities.GetEntityData(id)!.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Canvas"));
                    var settings = new BehaviourComponent(7302);
                    settings.AddProperty(SceneComponentBuilder.Vector(EngineBehavioursConstants.CANVAS_REFERENCE_RESOLUTION,
                        ("x", data["_referenceResolution"]!["x"]!.Value<float>()), ("y", data["_referenceResolution"]!["y"]!.Value<float>())));
                    settings.AddProperty(SerializedPropertyBuilder.Integer(EngineBehavioursConstants.CANVAS_SCALE_MODE, data["_scaleMode"]!.Value<int>()));
                    settings.AddProperty(new(EngineBehavioursConstants.CANVAS_MATCH_WIDTH_OR_HEIGHT, SerializedTypeEnum.Float, data["_matchWidthOrHeight"]!.Value<float>(), "float", null));
                    result.AddBehaviour(settings);
                }
                return result;
            }

            float[] ReadScreenBounds(int index = 0)
            {
                var result = new float[4];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectScreenBounds", index, result);
                return result;
            }

            float[] Bounds((float[][] Rects, int[] Parents, int[] Orders) state, int index)
            {
                var parentIndex = Array.IndexOf(ids, state.Parents[index]);
                var scale = state.Parents[index] == destinationCanvas.Id ? destinationScale : sourceScale;
                var parent = parentIndex < 0 ? new[] { 0f, 0f, 640 / scale, 480 / scale } : Bounds(state, parentIndex);
                var rect = state.Rects[index];
                var width = parent[2] - parent[0];
                var height = parent[3] - parent[1];
                var left = parent[0] + width * rect[4] + rect[0] - rect[8] * rect[2];
                var bottom = parent[1] + height * rect[5] + rect[1] - rect[9] * rect[3];
                return [left, bottom, left + width * (rect[6] - rect[4]) + rect[2], bottom + height * (rect[7] - rect[5]) + rect[3]];
            }

            float[] PixelBounds((float[][] Rects, int[] Parents, int[] Orders) state, int index)
            {
                var root = index;
                var parentIndex = Array.IndexOf(ids, state.Parents[root]);
                while (parentIndex >= 0)
                {
                    root = parentIndex;
                    parentIndex = Array.IndexOf(ids, state.Parents[root]);
                }
                var scale = state.Parents[root] == destinationCanvas.Id ? destinationScale : sourceScale;
                return Bounds(state, index).Select(value => value * scale).ToArray();
            }

            static void Check((float[][] Rects, int[] Parents, int[] Orders) expected, (float[][] Rects, int[] Parents, int[] Orders) actual)
            {
                Assert.Equal(expected.Parents, actual.Parents);
                Assert.Equal(expected.Orders, actual.Orders);
                for (var i = 0; i < expected.Rects.Length; i++) CheckFloats(expected.Rects[i], actual.Rects[i]);
            }

            static void CheckFloats(float[] expected, float[] actual)
            {
                Assert.Equal(expected.Length, actual.Length);
                for (var i = 0; i < expected.Length; i++) Assert.InRange(MathF.Abs(expected[i] - actual[i]), 0f, 0.0001f);
            }
        }
        finally
        {
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngineLoop");
            source.Disconnect();
            foreach (var editor in editors) editor.Dispose();
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task InspectorManualRectAnchorsShareHistoryWithPresetsAndHandles()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var field = 0; field < 4; field++) await VerifyInspectorManualRectAnchorAsync(field);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyInspectorManualRectAnchorAsync(int field)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var ids = new int[1];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", 3, 1, ids);
            using var inspector = new InspectorTransformFixture(entities, ids[0]);
            inspector.Apply(entities.GetEntityData(ids[0])!);
            var component = inspector.Entity.GetBehaviour(InspectorTransformFixture.RECT_TRANSFORM_ID)!;
            var registry = new TestBehaviourRegistry();
            registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = InspectorTransformFixture.RECT_TRANSFORM_ID;
            var parentId = Convert.ToInt32(inspector.Transform.GetProperty("_parent").Value);
            Assert.NotEqual(0, parentId);
            var parent = new GameEntity(parentId, "Canvas bounds");
            parent.AddBehaviour(SceneComponentBuilder.RectTransform(InspectorTransformFixture.RECT_TRANSFORM_ID,
                new(new(0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, 0.5f), new(0, 0), new(640, 480))));
            var scenes = new TestSceneManagementService(parent, inspector.Entity);
            Assert.True(scenes.Scene.Value!.MoveEntity(inspector.Entity, parent, 0));
            var layout = new RectTransformLayoutService(registry, scenes, null!);
            Assert.Equal(new RectTransformVector2(640, 480), layout.GetParentSize(inspector.Entity));
            using var vm = new RectTransformPropertyViewModel(inspector.Entity, component, inspector.Runner, entities, layout);
            var baseline = new float[20];
            api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, baseline);
            var initial = new[] { 20f, 30f, 200f, 160f, 0.5f, 0.5f, 0.5f, 0.5f };
            var initialBounds = new[] { 240f, 190f, 440f, 350f };
            var manual = initial.ToArray();
            manual[4 + field] = field < 2 ? 0.375f : 0.625f;
            var manualBounds = new[] { new[] { 160f, 190f, 440f, 350f }, new[] { 240f, 130f, 440f, 350f },
                new[] { 240f, 190f, 520f, 350f }, new[] { 240f, 190f, 440f, 410f } }[field];
            var stretched = new[] { new[] { -20f, 30f, -360f, -320f, 0f, 0f, 1f, 1f }, new[] { 20f, 0f, -440f, -260f, 0f, 0f, 1f, 1f },
                new[] { 60f, 30f, -360f, -320f, 0f, 0f, 1f, 1f }, new[] { 20f, 60f, -440f, -260f, 0f, 0f, 1f, 1f } }[field];
            var names = new[] { nameof(vm.AnchorMinX), nameof(vm.AnchorMinY), nameof(vm.AnchorMaxX), nameof(vm.AnchorMaxY) };
            var input = new ReiTextBox { DataContext = vm, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding(names[field]) { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "0";
            input.TextInternal = manual[4 + field].ToString(System.Globalization.CultureInfo.CurrentCulture);
            Check(initial, initialBounds);
            Assert.Empty(received);
            input.LoseFocus();
            Check(manual, manualBounds);
            Assert.Single(received);
            Assert.Equal("Custom", vm.AnchorModeName);
            var stretch = Assert.Single(vm.Presets, preset => preset.DisplayName == "Stretch Both");
            stretch.ApplyCommand.Execute(null);
            Check(stretched, manualBounds);
            Assert.True(stretch.IsSelected);
            Assert.Equal(2, received.Count);
            api.Invoke(typeof(EntityIdDelegate), "NativeProjectBeginRectResize", 2);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", 24, -16);
            var dragged = stretched.ToArray();
            dragged[0] += 12; dragged[1] += 8; dragged[2] += 24; dragged[3] += 16;
            var draggedBounds = new[] { manualBounds[0], manualBounds[1], manualBounds[2] + 24, manualBounds[3] + 16 };
            Check(dragged, draggedBounds);
            Assert.Equal(2, received.Count);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(dragged, draggedBounds);
            Assert.Equal(3, received.Count);
            await commands.UndoAsync();
            Check(stretched, manualBounds);
            await commands.UndoAsync();
            Check(manual, manualBounds);
            await commands.UndoAsync();
            Check(initial, initialBounds);
            Assert.False(commands.CanUndo);
            foreach (var text in new[] { 0.5f.ToString(System.Globalization.CultureInfo.CurrentCulture), "invalid", "NaN" })
            {
                input.TextInternal = text;
                input.LoseFocus();
                Check(initial, initialBounds);
                Assert.Equal(3, received.Count);
                Assert.True(commands.CanRedo);
            }
            await commands.RedoAsync();
            Check(manual, manualBounds);
            await commands.RedoAsync();
            Check(stretched, manualBounds);
            await commands.RedoAsync();
            Check(dragged, draggedBounds);
            Assert.Equal(3, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            await commands.UndoAsync();
            Check(manual, manualBounds);
            input.TextInternal = (field < 2 ? 0.25f : 0.75f).ToString(System.Globalization.CultureInfo.CurrentCulture);
            input.LoseFocus();
            var branched = initial.ToArray();
            branched[4 + field] = field < 2 ? 0.25f : 0.75f;
            var branchBounds = new[] { new[] { 80f, 190f, 440f, 350f }, new[] { 240f, 70f, 440f, 350f },
                new[] { 240f, 190f, 600f, 350f }, new[] { 240f, 190f, 440f, 470f } }[field];
            Check(branched, branchBounds);
            Assert.Equal(4, received.Count);
            Assert.False(commands.CanRedo);
            await commands.UndoAsync();
            Check(manual, manualBounds);
            await commands.RedoAsync();
            Check(branched, branchBounds);
            Assert.Equal(4, received.Count);
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void Check(float[] expected, float[] expectedBounds)
            {
                var native = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", 0, native);
                inspector.Apply(entities.GetEntityData(ids[0])!);
                Dispatcher.UIThread.RunJobs();
                Assert.True(layout.TryReadLayout(component, out var state));
                var editor = new[] { state.AnchoredPosition.X, state.AnchoredPosition.Y, state.SizeDelta.X, state.SizeDelta.Y,
                    vm.AnchorMinX, vm.AnchorMinY, vm.AnchorMaxX, vm.AnchorMaxY };
                for (var i = 0; i < 8; i++)
                {
                    Assert.InRange(MathF.Abs(native[i] - expected[i]), 0f, 0.0001f);
                    Assert.InRange(MathF.Abs(editor[i] - expected[i]), 0f, 0.0001f);
                }
                var left = 640 * native[4] + native[0] - native[8] * native[2];
                var bottom = 480 * native[5] + native[1] - native[9] * native[3];
                var actualBounds = new[] { left, bottom, left + 640 * (native[6] - native[4]) + native[2], bottom + 480 * (native[7] - native[5]) + native[3] };
                for (var i = 0; i < 4; i++) Assert.InRange(MathF.Abs(actualBounds[i] - expectedBounds[i]), 0f, 0.0001f);
                for (var i = 8; i < native.Length; i++) Assert.InRange(MathF.Abs(native[i] - baseline[i]), 0f, 0.0001f);
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public Task RectTransformPointerMovesShareHistoryWithoutChangingOtherFields()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var kind = 0; kind < 4; kind++) await VerifyRectPointerDragAsync(kind);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformPointerResizesShareHistoryForEveryEdgeAndCorner()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var index = 0; index < 8; index++) await VerifyRectPointerDragAsync(3, index);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformPointerGroupMovesShareOneHistoryEntryForArrowsPlaneAndBody()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var kind = 0; kind < 4; kind++) await VerifyRectPointerDragAsync(kind, targetCount: 3);
            return true;
        }, CancellationToken.None);
    }

    [NativeCallbackFact]
    public Task RectTransformPointerGroupResizesShareOneHistoryEntryForEveryEdgeAndCorner()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            for (var index = 0; index < 8; index++) await VerifyRectPointerDragAsync(3, index, targetCount: 3);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyRectPointerDragAsync(int kind, int? resizeHandle = null, int targetCount = 1)
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, new TestLogger<EngineApi>());
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var entities = new EntityApi(api);
        using var commands = new EditorCommandService();
        var logger = new TestLogger<NativeEditorHistoryRecorder>();
        using var recorder = new NativeEditorHistoryRecorder(api, new TestEngineNativeAccess(api), commands, logger);
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources => api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
        try
        {
            api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartRectMoveEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
            api.MarkEngineStarted();
            var entityIds = new int[targetCount];
            api.Invoke(typeof(PrepareRectMoveDelegate), "NativeProjectPrepareRectMove", kind, targetCount, entityIds);
            var entityId = entityIds[0];
            var before = Enumerable.Range(0, targetCount).Select(index => Read(index, 20 + index * 40, 30 + index * 25)).ToArray();
            var beginDrag = resizeHandle.HasValue ? "NativeProjectBeginRectResize" : "NativeProjectBeginRectMove";
            api.Invoke(typeof(EntityIdDelegate), beginDrag, resizeHandle ?? kind);
            api.Invoke(typeof(MoveRectPointerDelegate), "NativeProjectMoveRectPointer", kind == 1 ? 0 : 24, kind == 0 ? 0 : -16);
            var x = kind == 1 ? 20f : 44f;
            var y = kind == 0 ? 30f : 46f;
            var width = 200f;
            var height = 160f;
            if (resizeHandle is int index)
            {
                var directionX = new[] { -1, 0, 1, -1, 1, -1, 0, 1 }[index];
                var directionY = new[] { 1, 1, 1, 0, 0, -1, -1, -1 }[index];
                x = directionX == 0 ? 20f : 32f;
                y = directionY == 0 ? 30f : 38f;
                width += directionX * 24;
                height += directionY * 16;
            }
            Check(x, y, width, height);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(received);
            Assert.False(commands.CanUndo);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(x, y, width, height);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            await commands.UndoAsync();
            Check(20, 30);
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            api.Invoke(typeof(EntityIdDelegate), beginDrag, resizeHandle ?? kind);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectEndRectMove");
            Check(20, 30);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            Check(x, y, width, height);

            var entity = new GameEntity(entityId, "Rect move");
            var management = new TestEntityManagementService { OnRename = (target, name) => { entities.Rename(target.Id, name); target.SetName(name); } };
            var scenes = new TestSceneManagementService(entity);
            var runner = new TestEngineRunner { ActiveMode = EngineRunMode.EditorMode };
            runner.Active.Value = true;
            runner.EditorActive.Value = true;
            var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(entity, "Renamed"));
            Assert.Equal("Renamed", entities.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Rect move", entities.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            Check(20, 30);
            Assert.False(commands.CanUndo);
            await commands.RedoAsync();
            Check(x, y, width, height);
            await commands.RedoAsync();
            Assert.Equal("Renamed", entities.GetEntityData(entityId)!.Name);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.False(commands.CanRedo);
            Assert.Empty(logger.Entries);

            float[] Read(int index, float expectedX, float expectedY, float expectedWidth = 200, float expectedHeight = 160)
            {
                var values = new float[20];
                api.Invoke(typeof(ReadRectMoveDelegate), "NativeProjectReadRectMove", index, values);
                Assert.InRange(MathF.Abs(values[0] - expectedX), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(values[1] - expectedY), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(values[2] - expectedWidth), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(values[3] - expectedHeight), 0f, 0.0001f);
                var state = entities.GetEntityData(entityIds[index])!;
                var rect = JObject.FromObject(state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "RectTransform"));
                Assert.InRange(MathF.Abs(rect["_anchoredPosition"]!["x"]!.Value<float>() - values[0]), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(rect["_anchoredPosition"]!["y"]!.Value<float>() - values[1]), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(rect["_sizeDelta"]!["x"]!.Value<float>() - values[2]), 0f, 0.0001f);
                Assert.InRange(MathF.Abs(rect["_sizeDelta"]!["y"]!.Value<float>() - values[3]), 0f, 0.0001f);
                return values;
            }

            void Check(float expectedX, float expectedY, float expectedWidth = 200, float expectedHeight = 160)
            {
                for (var index = 0; index < targetCount; index++)
                {
                    var values = Read(index, expectedX + index * 40, expectedY + index * 25, expectedWidth, expectedHeight);
                    for (var i = 4; i < values.Length; i++) Assert.InRange(MathF.Abs(before[index][i] - values[i]), 0f, 0.0001f);
                }
            }
        }
        finally
        {
            api.MarkEngineStopped();
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
            GC.KeepAlive(createEngine);
        }
    }

    [NativeCallbackFact]
    public void NativeProjectCallbackPreservesUInt64SurvivesGcAndReconnectsWithoutDuplicates()
    {
        Assert.Equal("1", Environment.GetEnvironmentVariable("REI_NATIVE_TEST_CHILD"));
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build the shared native project fixture and set REI_TEST_NATIVE_PROJECT_DLL.");
        var logger = new TestLogger<EngineApi>();
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, logger);
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        CreateActionEngineDelegate createEngine = resources =>
        {
            var engine = api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
            Assert.Equal(0, api.Invoke<int>(typeof(ReplayActionDelegate), "UndoEditorAction", 1UL));
            Assert.Equal(0, api.Invoke<int>(typeof(ReplayActionDelegate), "RedoEditorAction", 1UL));
            return engine;
        };
        try
        {
            ulong[] expected = [1, (ulong)uint.MaxValue + 1, (1UL << 63) + 17, ulong.MaxValue];
            for (var run = 0; run < 2; run++)
            {
                received.Clear();
                try
                {
                    api.Invoke(typeof(StartActionEngineDelegate), "NativeProjectStartActionEngine", Marshal.GetFunctionPointerForDelegate(createEngine));
                    api.MarkEngineStarted();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    foreach (var id in expected) api.Invoke(typeof(EmitActionDelegate), "NativeProjectEmitActionRecorded", id);
                    Assert.Equal(expected, received);
                    Action<ulong> failingSubscriber = _ => throw new InvalidOperationException("subscriber failure");
                    api.ActionRecorded += failingSubscriber;
                    api.Invoke(typeof(EmitActionDelegate), "NativeProjectEmitActionRecorded", 9UL);
                    api.ActionRecorded -= failingSubscriber;
                    Assert.Equal(9UL, received[^1]);
                    Assert.Equal(run + 1, logger.Entries.Count(entry => entry.Exception?.Message == "subscriber failure"));
                    api.Invoke(typeof(EmitActionDelegate), "NativeProjectEmitActionRecorded", 10UL);
                    Assert.Equal(10UL, received[^1]);
                    Assert.Equal(expected.Length + 2, received.Count);
                }
                finally
                {
                    api.MarkEngineStopped();
                    api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectStopActionEngine");
                }
            }
        }
        finally { GC.KeepAlive(createEngine); }
    }
}
