using Avalonia;
using Avalonia.Data;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Tests.Infrastructure.Fixtures;
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
    public Task InspectorPositionConfirmationSharesHistoryWithNativeActionAndRename()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () => { await VerifyInspectorPositionAsync(); return true; }, CancellationToken.None);
    }

    private static async Task VerifyInspectorPositionAsync()
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
            var entityId = entityApi.CreateNewEntity("Inspector Position")?.EntityId ?? throw new InvalidOperationException("Could not create native entity.");
            using var inspector = new InspectorPositionFixture(entityApi, entityId);
            RefreshAndCheckPosition(0);
            var input = new ReiTextBox { DataContext = inspector.ViewModel, IsFloat = true };
            using var binding = input.Bind(ReiTextBox.TextProperty, new Binding("X") { Mode = BindingMode.TwoWay });
            Dispatcher.UIThread.RunJobs();
            input.TextInternal = "1";
            input.TextInternal = "12.0";
            RefreshAndCheckPosition(0);
            Assert.Empty(received);
            input.LoseFocus();
            RefreshAndCheckPosition(12);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanUndo);
            var positionActionId = received[0];

            await commands.UndoAsync();
            RefreshAndCheckPosition(0);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            input.TextInternal = "0";
            input.LoseFocus();
            // Also verify the native no-op guard when a caller explicitly sends the unchanged value.
            entityApi.SetData(new SetEntityDataRequest
            {
                SceneId = entityId,
                RecordPositionChange = true,
                Behaviours = [new() { [SetEntityDataRequest.REI_BEHAVIOUR_ID] = InspectorPositionFixture.TRANSFORM_ID,
                    ["_position"] = SerializedPropertyJsonConverter.SerializeRuntimeProperty(inspector.Transform.GetProperty("_position")) }]
            });
            RefreshAndCheckPosition(0);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(received);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            RefreshAndCheckPosition(12);

            var nativeActionId = api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", 0, 1);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { positionActionId, nativeActionId }, received);
            var entities = new TestEntityManagementService { OnRename = (entity, name) => { entityApi.Rename(entity.Id, name); entity.SetName(name); } };
            var scenes = new TestSceneManagementService(inspector.Entity);
            var rename = new EntityRenameCommand(entities, new TestLogger<EntityRenameCommand>(), scenes, inspector.Runner);
            await commands.ExecuteAsync((IEntityRenameCommand)rename, new(inspector.Entity, "Renamed"));
            Assert.Equal("Renamed", entityApi.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            Assert.Equal("Inspector Position", entityApi.GetEntityData(entityId)!.Name);
            await commands.UndoAsync();
            AssertNativeActionPosition(1, 2, 3);
            RefreshAndCheckPosition(12);
            await commands.UndoAsync();
            RefreshAndCheckPosition(0);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            await commands.RedoAsync();
            RefreshAndCheckPosition(12);
            await commands.RedoAsync();
            AssertNativeActionPosition(4, 5, 6);
            await commands.RedoAsync();
            Assert.Equal("Renamed", entityApi.GetEntityData(entityId)!.Name);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanRedo);
            Assert.Equal(new[] { positionActionId, nativeActionId }, received);
            Assert.Empty(logger.Entries);
            Assert.Empty(inspector.Logger.Entries);

            void RefreshAndCheckPosition(float expectedX)
            {
                var state = entityApi.GetEntityData(entityId)!;
                Assert.NotNull(state);
                var transform = state.Behaviours.Single(item => item["REI_TYPE"].ToString() == "Transform");
                var position = JObject.FromObject(transform["_position"]);
                Assert.Equal(expectedX, position["x"]!.Value<float>());
                Assert.Equal(0f, position["y"]!.Value<float>());
                Assert.Equal(0f, position["z"]!.Value<float>());
                var scale = JObject.FromObject(transform["_scale"]);
                var rotation = JObject.FromObject(transform["_rotation"]);
                foreach (var axis in new[] { "x", "y", "z" })
                {
                    Assert.Equal(1f, scale[axis]!.Value<float>());
                    Assert.Equal(0f, rotation[axis]!.Value<float>());
                }
                inspector.Apply(state);
                Assert.Equal(expectedX, inspector.ViewModel.X);
                Assert.Equal(expectedX, Convert.ToSingle(((Dictionary<string, SerializedProperty>)inspector.Transform.GetProperty("_position").Value!)["x"].Value));
                Assert.Equal(0f, inspector.ViewModel.Y);
                Assert.Equal(0f, inspector.ViewModel.Z);
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
