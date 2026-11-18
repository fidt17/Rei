using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Scenes;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "SceneManagement")]
public sealed class EngineSceneLoaderTests
{
    private sealed class NativeApi : TestEngineApi, IEngineNativeAccess
    {
        public Func<bool> Load { get; set; } = () => true;
        public object? InstanceToken { get; } = new object();

        public bool TryInvoke(Action<IEngineApi> action) { action(this); return true; }
        public override bool LoadEditorScene(string json) => Load();
    }

    [AvaloniaTheory]
    [InlineData("loaded")]
    [InlineData("failed")]
    [InlineData("exception")]
    public async Task PendingLayoutFinishesBeforeNativeLoadAndUiRemainsResponsive(string outcome)
    {
        var layoutApplied = false;
        var nativeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishNative = new ManualResetEventSlim();
        var api = new NativeApi
        {
            Load = () =>
            {
                Assert.False(Dispatcher.UIThread.CheckAccess());
                Assert.True(layoutApplied);
                nativeStarted.SetResult();
                Assert.True(finishNative.Wait(TimeSpan.FromSeconds(5)));
                if (outcome == "exception") throw new InvalidOperationException("Native scene load failed.");
                return outcome == "loaded";
            }
        };
        var logger = new TestLogger<EngineSceneLoader>();
        var loader = new EngineSceneLoader(api, logger);
        Dispatcher.UIThread.Post(() => layoutApplied = true, DispatcherPriority.Render);

        var loading = loader.LoadSceneAsync(null);
        try
        {
            await nativeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(loading.IsCompleted);
            var uiResponded = false;
            await Dispatcher.UIThread.InvokeAsync(() => uiResponded = true, DispatcherPriority.Background);
            Assert.True(uiResponded);
            Assert.False(loading.IsCompleted);
        }
        finally
        {
            finishNative.Set();
        }
        Assert.Equal(outcome == "loaded", await loading);
        Assert.Equal(outcome == "exception" ? 1 : 0, logger.Entries.Count);
    }

    /// <summary>Native progress arrives while loading; subscriptions disappear after success, failure or exception.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeProgressIsForwardedOnlyDuringTheLoad(bool throws)
    {
        var values = new List<int>();
        var api = new NativeApi();
        api.Load = () =>
        {
            api.EmitSceneLoadProgress(0);
            api.EmitSceneLoadProgress(43);
            if (throws) throw new InvalidOperationException("failed");
            api.EmitSceneLoadProgress(100);
            return true;
        };
        var loader = new EngineSceneLoader(api, new TestLogger<EngineSceneLoader>());
        Assert.Equal(!throws, await loader.LoadSceneAsync(null, values.Add));
        api.EmitSceneLoadProgress(79);
        Assert.Equal(throws ? new[] { 0, 43 } : new[] { 0, 43, 100 }, values);
    }
}
