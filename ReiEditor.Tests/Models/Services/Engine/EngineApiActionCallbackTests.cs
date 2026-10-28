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
