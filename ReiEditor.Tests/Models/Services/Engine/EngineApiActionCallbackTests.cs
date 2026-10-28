using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Hierarchies;
using ReiEditor.Models.Services.RectTransform;
using Avalonia;
using Avalonia.Data;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Tests.Infrastructure.Fixtures;
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

namespace ReiEditor.Tests.Models.Services.Engine;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Area", "EngineActionCallbacks")]
public sealed class EngineApiActionCallbackTests
{
    private sealed class NativeCallbackFactAttribute : FactAttribute
    {
        public NativeCallbackFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Native callbacks require Windows x64.";
            else if (Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1")
                Skip = "Set REI_RUN_ENGINE_TESTS=1 and REI_TEST_NATIVE_PROJECT_DLL to the built native project fixture.";
        }
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

    private static async Task VerifyHierarchyDragAsync(bool nested)
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
            for (var i = 0; i < 3; i++)
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
            CheckOrders([0, 1, 2]);
            Assert.Empty(received);

            drag.ViewModel.MoveEntities([editors[0].Entity.Id], nested ? parentId : null, 3);
            CheckOrders([2, 0, 1]);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            await commands.UndoAsync();
            CheckOrders([0, 1, 2]);
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            // Dropping in the same slot must preserve Redo and produce no callback.
            drag.ViewModel.MoveEntities([editors[0].Entity.Id], nested ? parentId : null, 1);
            CheckOrders([0, 1, 2]);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            CheckOrders([2, 0, 1]);

            api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", 0, 1);
            Dispatcher.UIThread.RunJobs();
            var rename = new EntityRenameCommand(management, new TestLogger<EntityRenameCommand>(), scenes, runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(editors[0].Entity, "Renamed"));
            Assert.Equal("Renamed", entityApi.GetEntityData(editors[0].Entity.Id)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Sibling 0", entityApi.GetEntityData(editors[0].Entity.Id)!.Name);
            await commands.UndoAsync();
            CheckMove(1);
            CheckOrders([2, 0, 1]);
            await commands.UndoAsync();
            CheckOrders([0, 1, 2]);
            Assert.False(commands.CanUndo);
            await commands.RedoAsync();
            CheckOrders([2, 0, 1]);
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

    private static async Task VerifyHierarchyReparentAsync(int mode)
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
            var target = editors[2];
            var oldParent = mode == 2 ? 0 : editors[0].Entity.Id;
            var newParent = mode == 1 ? 0 : editors[1].Entity.Id;
            SetTransform(editors[0].Entity.Id, 10, 2, 30);
            SetTransform(editors[1].Entity.Id, -5, 3, -45);
            SetTransform(target.Entity.Id, 1, 1.5f, 15);
            for (var i = 2; i < 7; i++)
                entityApi.SetEntityParent(editors[i].Entity.Id, i < 5 ? oldParent : newParent, i < 5 ? i - 2 : i - 5);
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

            drag.ViewModel.MoveEntities([target.Entity.Id], newParent == 0 ? null : newParent, 1);
            var after = await ReadStateAsync();
            Assert.Equal(newParent, after[target.Entity.Id]["_parent"]!.Value<int>());
            Assert.Equal(1, after[target.Entity.Id]["_order"]!.Value<int>());
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
            drag.ViewModel.MoveEntities([target.Entity.Id], oldParent == 0 ? null : oldParent, originalIndex + 1);
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
            drag.ViewModel.MoveEntities([target.Entity.Id], oldParent == 0 ? null : oldParent, 0);
            var rectState = await ReadStateAsync();
            Assert.Equal(oldParent, rectState[target.Entity.Id]["_parent"]!.Value<int>());
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

            float[] ReadWorld()
            {
                var values = new float[10];
                api.Invoke(typeof(ReadEntityWorldTransformDelegate), "NativeProjectReadEntityWorldTransform", target.Entity.Id, (object)values);
                return values;
            }

            void CheckWorld(float[] expected)
            {
                var actual = ReadWorld();
                for (var i = 0; i < 6; i++) Assert.InRange(MathF.Abs(expected[i] - actual[i]), 0f, 0.0001f);
                var dot = Enumerable.Range(6, 4).Sum(i => expected[i] * actual[i]);
                Assert.InRange(MathF.Abs(MathF.Abs(dot) - 1), 0f, 0.0001f);
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
