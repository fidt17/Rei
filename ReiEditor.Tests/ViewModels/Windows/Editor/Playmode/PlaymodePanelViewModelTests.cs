using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common.Condition;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Playmode;
using ReiEditor.ViewModels.Windows.Editor.Playmode.Commands;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Playmode;

/// <summary>Verifies playmode panel ownership, engine state, window pointer, and command lifecycles.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "ViewModels")]
public sealed class PlaymodePanelViewModelTests
{
    /// <summary>Tracks observable subscribers and publishes controlled values.</summary>
    private sealed class TestObservable<T>(T value) : ReiEditor.Utils.Common.IObservable<T>
    {
        private readonly List<Action<T>> _subscribers = new();
        public T Value { get; private set; } = value;
        public int SubscriberCount => _subscribers.Count;

        public void Subscribe(Action<T> callback, bool invoke = true)
        {
            _subscribers.Add(callback);
            if (invoke) callback(Value);
        }

        public void Unsubscribe(Action<T> callback) => _subscribers.Remove(callback);

        public void Publish(T next)
        {
            Value = next;
            foreach (var subscriber in _subscribers.ToArray()) subscriber(next);
        }
    }

    /// <summary>Exposes controlled engine states and records stop requests.</summary>
    private sealed class TestEngineRunner : IEngineRunner
    {
        public event Action? EngineStartedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public event Action? EngineStartFailedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public TestObservable<bool> Active { get; } = new(false);
        public TestObservable<bool> EditorActive { get; } = new(false);
        public TestObservable<bool> PlaymodeActive { get; } = new(false);
        public TestObservable<bool> Starting { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsActive => Active;
        public ReiEditor.Utils.Common.IObservable<bool> IsEditorActive => EditorActive;
        public ReiEditor.Utils.Common.IObservable<bool> IsPlaymodeActive => PlaymodeActive;
        public ReiEditor.Utils.Common.IObservable<bool> IsEngineStarting => Starting;
        public EngineRunMode ActiveMode => EngineRunMode.EditorMode;
        public int StopCount { get; private set; }
        public TaskCompletionSource StopRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StartEngine(EngineRunMode mode) => throw new NotSupportedException();
        public Task StopEngine()
        {
            StopCount++;
            StopRequested.TrySetResult();
            return Task.CompletedTask;
        }
    }

    /// <summary>Exposes controlled native-window pointer without creating a real window.</summary>
    private sealed class TestEngineWindowController : IEngineWindowController
    {
        public TestObservable<IntPtr?> Pointer { get; } = new(null);
        public ReiEditor.Utils.Common.IObservable<IntPtr?> WindowPointer => Pointer;
        public ReiEditor.Utils.Common.IObservable<(int Width, int Height)?> ViewportSize { get; } = new TestObservable<(int Width, int Height)?>(null);
        public void SetupWindow() { }
        public void DestroyWindow() { }
        public void SetViewportSize(int width, int height) { }
    }

    /// <summary>Maps managed engine pointers to deterministic window handles.</summary>
    private sealed class TestWindowEngineApi : TestEngineApi
    {
        public override IntPtr GetWindowHandle(IntPtr windowPtr) => windowPtr + 100;
    }

    /// <summary>Wraps controlled command availability.</summary>
    private sealed class TestCondition(bool initial) : ICondition
    {
        public TestObservable<bool> State { get; } = new(initial);
        public ReiEditor.Utils.Common.IObservable<bool> IsTrue => State;
        public void Dispose() { }
    }

    /// <summary>Supplies controlled start availability.</summary>
    private sealed class TestPlaymodeStarter(bool initial) : IPlaymodeStarter
    {
        public TestCondition Condition { get; } = new(initial);
        public ICondition CanStart => Condition;
        public bool TryStart() => throw new NotSupportedException();
    }

    /// <summary>Signals background workflow invocation and holds completion under test control.</summary>
    private sealed class TestPlaymodeStartWorkflow : IPlaymodeStartWorkflow
    {
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return _completion.Task;
        }

        public void Complete(bool result) => _completion.TrySetResult(result);
    }

    /// <summary>Returns one fixed dependency from both factory overloads.</summary>
    private sealed class TestFactory<T>(T value) : IFactory<T> where T : class
    {
        public T CreateInstance() => value;
        public T CreateInstance(params object[] parameters) => value;
    }

    /// <summary>Tracks disposal of render mode child VM.</summary>
    private sealed class TestRenderModeSelectionViewModel : RenderModeSelectionViewModel
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }

    /// <summary>Tracks disposal of editor grid child VM.</summary>
    private sealed class TestEditorGridOptionsViewModel : EditorGridOptionsViewModel
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }

    /// <summary>Tracks disposal of UI render child VM.</summary>
    private sealed class TestUiRenderOptionsViewModel : UIRenderOptionsViewModel
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }

    /// <summary>Tracks disposal of transformation controls child VM.</summary>
    private sealed class TestTransformationControlSettingsViewModel : TransformationControlSettingsViewModel
    {
        public int DisposeCount { get; private set; }
        public override void Dispose() => DisposeCount++;
    }

    /// <summary>Initial subscriptions mirror state and disposal releases owned commands and child VMs.</summary>
    [AvaloniaFact]
    public void TestPanelMirrorsModeFlagsAndDisposesOwnedDependencies()
    {
        var runner = new TestEngineRunner();
        runner.Active.Publish(true);
        runner.EditorActive.Publish(true);
        var render = new TestRenderModeSelectionViewModel();
        var grid = new TestEditorGridOptionsViewModel();
        var ui = new TestUiRenderOptionsViewModel();
        var transformations = new TestTransformationControlSettingsViewModel();
        var panel = TestCreatePanel(runner, new TestEngineWindowController(), render, grid, ui, transformations);

        Assert.True(panel.EngineActive);
        Assert.True(panel.EditorModeActive);
        Assert.False(panel.PlayModeActive);
        runner.PlaymodeActive.Publish(true);
        Assert.True(panel.PlayModeActive);

        panel.Dispose();

        Assert.Equal(1, render.DisposeCount);
        Assert.Equal(1, grid.DisposeCount);
        Assert.Equal(1, ui.DisposeCount);
        Assert.Equal(1, transformations.DisposeCount);
        Assert.Equal(0, runner.PlaymodeActive.SubscriberCount);
        Assert.Equal(0, runner.EditorActive.SubscriberCount);
    }

    /// <summary>Disposed panel releases all engine-state subscriptions and ignores later state changes.</summary>
    [AvaloniaFact]
    public void TestDisposeUnsubscribesEngineActiveState()
    {
        var runner = new TestEngineRunner();
        var panel = TestCreatePanel(runner, new TestEngineWindowController());

        panel.Dispose();
        runner.Active.Publish(true);
        runner.EditorActive.Publish(true);
        runner.PlaymodeActive.Publish(true);

        Assert.False(panel.EngineActive);
        Assert.False(panel.EditorModeActive);
        Assert.False(panel.PlayModeActive);
        Assert.Equal(0, runner.Active.SubscriberCount);
        Assert.Equal(0, runner.EditorActive.SubscriberCount);
        Assert.Equal(0, runner.PlaymodeActive.SubscriberCount);
    }

    /// <summary>Window pointer creates/removes provider and later pointer updates are ignored after disposal.</summary>
    [AvaloniaFact]
    public async Task TestWindowPointerChangesProviderUntilDispose()
    {
        var runner = new TestEngineRunner();
        var window = new TestEngineWindowController();
        var panel = TestCreatePanel(runner, window);

        window.Pointer.Publish(new IntPtr(23));
        await Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.NotNull(panel.WindowProvider);
        Assert.Equal(new IntPtr(123), panel.WindowProvider.WindowHandlePointer);

        window.Pointer.Publish(null);
        await Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.Null(panel.WindowProvider);
        panel.Dispose();
        window.Pointer.Publish(new IntPtr(50));
        await Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.Null(panel.WindowProvider);
        Assert.Equal(0, window.Pointer.SubscriberCount);
    }

    /// <summary>Start command reflects guard, dispatches workflow, and stops notifications after disposal.</summary>
    [AvaloniaFact]
    public async Task TestStartCommandGuardDispatchAndDispose()
    {
        var starter = new TestPlaymodeStarter(false);
        var workflow = new TestPlaymodeStartWorkflow();
        var command = new StartPlaymodeCommand(starter, workflow);
        var changes = 0;
        command.CanExecuteChanged += (_, _) => changes++;
        Assert.False(command.CanExecute(null));

        starter.Condition.State.Publish(true);
        Assert.True(command.CanExecute(null));
        Assert.Equal(1, changes);
        command.Execute(null);
        try
        {
            await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            workflow.Complete(true);

            command.Dispose();
            starter.Condition.State.Publish(false);
            Assert.Equal(1, changes);
            Assert.Equal(0, starter.Condition.State.SubscriberCount);
        }
        finally
        {
            workflow.Complete(false);
            command.Dispose();
        }
    }

    /// <summary>Stop command reflects playmode guard, invokes stop once, and unsubscribes on disposal.</summary>
    [AvaloniaFact]
    public async Task TestStopCommandGuardExecutionAndDispose()
    {
        var runner = new TestEngineRunner();
        var command = new StopPlaymodeCommand(runner);
        var changes = 0;
        command.CanExecuteChanged += (_, _) => changes++;
        Assert.False(command.CanExecute(null));

        runner.PlaymodeActive.Publish(true);
        Assert.True(command.CanExecute(null));
        Assert.Equal(1, changes);
        command.Execute(null);
        await runner.StopRequested.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, runner.StopCount);

        command.Dispose();
        runner.PlaymodeActive.Publish(false);
        Assert.Equal(1, changes);
        Assert.Equal(0, runner.PlaymodeActive.SubscriberCount);
    }

    /// <summary>Creates panel with real commands and harmless tracking child VMs.</summary>
    private static PlaymodePanelViewModel TestCreatePanel(
        TestEngineRunner runner,
        TestEngineWindowController window,
        TestRenderModeSelectionViewModel? render = null,
        TestEditorGridOptionsViewModel? grid = null,
        TestUiRenderOptionsViewModel? ui = null,
        TestTransformationControlSettingsViewModel? transformations = null)
    {
        var starter = new TestPlaymodeStarter(true);
        var start = new StartPlaymodeCommand(starter, new TestPlaymodeStartWorkflow());
        var stop = new StopPlaymodeCommand(runner);
        return new PlaymodePanelViewModel(
            new TestFactory<StartPlaymodeCommand>(start),
            new TestFactory<StopPlaymodeCommand>(stop),
            window,
            new TestFactory<RenderModeSelectionViewModel>(render ?? new TestRenderModeSelectionViewModel()),
            new TestFactory<EditorGridOptionsViewModel>(grid ?? new TestEditorGridOptionsViewModel()),
            new TestFactory<UIRenderOptionsViewModel>(ui ?? new TestUiRenderOptionsViewModel()),
            new TestFactory<TransformationControlSettingsViewModel>(transformations ?? new TestTransformationControlSettingsViewModel()),
            new TestWindowEngineApi(),
            runner);
    }
}
