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
    private delegate IntPtr PrepareResourcesDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EmitActionDelegate(ulong id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateActionEngineDelegate(IntPtr resources);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StartActionEngineDelegate(IntPtr createEngine);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong RecordTransformActionDelegate(int mode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReadTransformDelegate([Out] float[] values);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ReplayActionDelegate(ulong id);

    [NativeCallbackFact]
    public Task NativeCallbacksRecordTransformActionsInCommonHistoryAndReplayWithoutRecordingAgain()
    {
        var ui = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly);
        return ui.Dispatch(async () =>
        {
            foreach (var mode in new[] { 0, 1, 2 }) await VerifyNativeCallbackHistoryAsync(mode);
            return true;
        }, CancellationToken.None);
    }

    private static async Task VerifyNativeCallbackHistoryAsync(int mode)
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
            var id = api.Invoke<ulong>(typeof(RecordTransformActionDelegate), "NativeProjectRecordTransformAction", mode);
            Assert.NotEqual(0UL, id);
            Assert.Equal(new[] { id }, received);
            float[] before = [1, 2, 3, 1, 1, 1, 0, 0, 0, 1];
            var after = (float[])before.Clone();
            if (mode == 0) { after[0] = 4; after[1] = 5; after[2] = 6; }
            else if (mode == 1) { after[3] = 2; after[4] = 3; after[5] = 4; }
            else { after[8] = MathF.Sqrt(0.5f); after[9] = MathF.Sqrt(0.5f); }
            AssertTransform(after);
            Dispatcher.UIThread.RunJobs();
            Assert.True(commands.CanUndo);
            await commands.UndoAsync();
            AssertTransform(before);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanUndo);
            Assert.True(commands.CanRedo);
            await commands.RedoAsync();
            AssertTransform(after);
            Dispatcher.UIThread.RunJobs();
            Assert.False(commands.CanRedo);
            foreach (var missing in new[] { 0UL, ulong.MaxValue })
            {
                Assert.False(api.UndoEditorAction(missing));
                Assert.False(api.RedoEditorAction(missing));
            }
            AssertTransform(after);
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

        void AssertTransform(float[] expected)
        {
            var values = new float[10];
            api.Invoke(typeof(ReadTransformDelegate), "NativeProjectReadActionTransform", (object)values);
            for (var i = 0; i < values.Length; i++)
                Assert.True(MathF.Abs(expected[i] - values[i]) < 0.00001f, $"Mode {mode}, component {i}: expected {expected[i]}, actual {values[i]}.");
        }
    }

    [NativeCallbackFact]
    public void NativeProjectCallbackPreservesUInt64SurvivesGcAndReconnectsWithoutDuplicates()
    {
        var path = Environment.GetEnvironmentVariable("REI_TEST_NATIVE_PROJECT_DLL");
        Assert.True(File.Exists(path), "Build Rei/tests/ProjectDll/NativeProject.vcxproj and set REI_TEST_NATIVE_PROJECT_DLL.");
        var logger = new TestLogger<EngineApi>();
        var factory = new AssetBuildEngineSessionFactory(null!, null!, null!, logger);
        using var session = factory.CreateIsolatedSession(path!);
        var api = session.EngineApi;
        Assert.True(api.HasExport("NativeProjectPrepareActionResources"), "Rebuild the native project fixture with the callback test helpers.");
        Assert.True(api.HasExport("AddEditorActionRecordedCallback"), "Rebuild the native project fixture with the current Editor API.");
        var received = new List<ulong>();
        api.ActionRecorded += received.Add;
        var initialDirectory = Directory.GetCurrentDirectory();
        var resources = api.Invoke<IntPtr>(typeof(PrepareResourcesDelegate), "NativeProjectPrepareActionResources");
        try
        {
            ulong[] expected = [1, (ulong)uint.MaxValue + 1, (1UL << 63) + 17, ulong.MaxValue];
            for (var run = 0; run < 2; run++)
            {
                received.Clear();
                var engine = api.CreateEngine(Marshal.PtrToStringUTF8(resources)!, EngineRunMode.EditorMode);
                try
                {
                    Assert.Equal(0, api.Invoke<int>(typeof(ReplayActionDelegate), "UndoEditorAction", 1UL));
                    Assert.Equal(0, api.Invoke<int>(typeof(ReplayActionDelegate), "RedoEditorAction", 1UL));
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
                finally { api.DestroyEngine(engine); }
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(initialDirectory);
            api.Invoke(typeof(IEngineApi.VoidCallbackDelegate), "NativeProjectReleaseActionResources");
        }
    }
}
