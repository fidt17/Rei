using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.StatusBar;

/// <summary>Verifies active procedure display, completion order and disposal boundaries.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "StatusBar")]
public sealed class StatusBarViewModelTests
{
    /// <summary>Exposes completion subscription counts while retaining normal procedure behavior.</summary>
    private sealed class TestProcedure(string name) : IProcedure
    {
        public event Action? FinishedEvent;
        public event Action? ProgressChangedEvent { add { } remove { } }
        public double? Progress => null;
        public string Name { get; } = name;
        public bool Finished { get; private set; }
        public int SubscriberCount => FinishedEvent?.GetInvocationList().Length ?? 0;

        /// <summary>Completes the controlled procedure and publishes its completion.</summary>
        public void Complete()
        {
            Finished = true;
            FinishedEvent?.Invoke();
        }
    }

    /// <summary>Latest procedure wins; any completion selects the newest remaining procedure, then clears status.</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletionOrderPreservesRemainingProcedure(bool newestFirst)
    {
        var service = new EditorProceduresService();
        using var vm = new StatusBarViewModel(service);
        Assert.False(vm.ShowStatusBar);
        var first = new Procedure("First");
        var second = new Procedure("Second");
        service.TrackProcedure(first);
        service.TrackProcedure(second);
        Assert.True(vm.ShowStatusBar);
        Assert.Equal("Second...", vm.ActiveProcedureText);
        (newestFirst ? second : first).Complete();
        Assert.True(vm.ShowStatusBar);
        Assert.Equal(newestFirst ? "First..." : "Second...", vm.ActiveProcedureText);
        (newestFirst ? first : second).Complete();
        Assert.False(vm.ShowStatusBar);
        Assert.Empty(vm.ActiveProcedureText);
    }

    /// <summary>Disposal prevents notifications from procedures already tracked by the view model.</summary>
    [AvaloniaFact]
    public void DisposeDetachesRunningProcedureCompletion()
    {
        var service = new EditorProceduresService();
        var vm = new StatusBarViewModel(service);
        var procedure = new Procedure("Held");
        service.TrackProcedure(procedure);
        vm.Dispose();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        procedure.Complete();
        service.TrackProcedure(new Procedure("Later"));
        Assert.Empty(changes);
    }

    /// <summary>Disposal detaches every running procedure while another status view continues to follow completion order.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposedViewStaysUnchangedWhileLiveViewTracksProcedures(bool newestFirst)
    {
        var service = new EditorProceduresService();
        var disposed = new StatusBarViewModel(service);
        using var live = new StatusBarViewModel(service);
        var first = new Procedure("First");
        var second = new Procedure("Second");
        service.TrackProcedure(first);
        service.TrackProcedure(second);
        disposed.Dispose();
        var changes = new List<string?>();
        disposed.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        (newestFirst ? second : first).Complete();
        Assert.Equal(newestFirst ? "First..." : "Second...", live.ActiveProcedureText);
        (newestFirst ? first : second).Complete();
        Assert.False(live.ShowStatusBar);
        service.TrackProcedure(new Procedure("Later"));

        Assert.Equal("Later...", live.ActiveProcedureText);
        Assert.Equal("Second...", disposed.ActiveProcedureText);
        Assert.True(disposed.ShowStatusBar);
        Assert.Empty(changes);
    }

    /// <summary>Repeated creation and disposal leave no view-owned callbacks on a pending procedure.</summary>
    [AvaloniaFact]
    public void RepeatedViewLifetimesDoNotRetainProcedureSubscriptions()
    {
        var service = new EditorProceduresService();
        var pending = new TestProcedure("Pending");
        for (var index = 0; index < 3; index++)
        {
            var view = new StatusBarViewModel(service);
            if (index == 0) service.TrackProcedure(pending);
            var next = new TestProcedure("Next");
            service.TrackProcedure(next);
            view.Dispose();
            view.Dispose();

            Assert.Equal(1, pending.SubscriberCount);
            Assert.Equal(1, next.SubscriberCount);
            next.Complete();
        }
        pending.Complete();
        Assert.False(service.AnyActiveProcedures());
    }

    /// <summary>Background reports reach UI, preserve the spinner-only fallback, and never show premature 100%.</summary>
    [AvaloniaFact]
    public async Task WorkerProgressUpdatesTextAndResumesPreviousProcedure()
    {
        var service = new EditorProceduresService();
        using var vm = new StatusBarViewModel(service);
        var first = new Procedure("Load");
        service.TrackProcedure(first);
        Assert.Equal("Load...", vm.ActiveProcedureText);
        var allChangesOnUi = true;
        vm.PropertyChanged += (_, _) => allChangesOnUi &= Dispatcher.UIThread.CheckAccess();
        await Task.Run(() => first.ReportProgress(0.37));
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Equal("Load... 37%", vm.ActiveProcedureText);
        var second = new Procedure("Other");
        service.TrackProcedure(second);
        await Task.Run(() => first.ReportProgress(0.9999));
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Equal("Other...", vm.ActiveProcedureText);
        second.Complete();
        Assert.Equal("Load... 99%", vm.ActiveProcedureText);
        first.ReportProgress(1);
        Assert.Equal("Load... 100%", vm.ActiveProcedureText);
        vm.Dispose();
        first.Complete();
        Assert.Equal("Load... 100%", vm.ActiveProcedureText);
        Assert.True(allChangesOnUi);
    }
}
