using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.AssetCreation.Common;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.ViewModels.Controls;

/// <summary>Verifies single dialog ownership, duplicate suppression and cleanup after window failures.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Dialogs")]
public sealed class SingleDialogWindowCoordinatorTests
{
    private sealed class TestViewModel : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private sealed class TestMainWindowService : IMainWindowService
    {
        public event Action? ActivatedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public event Action? DeactivatedEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public Action<Window> OnShow { get; set; } = window => window.Show();
        public Window GetMainWindow() => throw new NotSupportedException();
        public void ShowMainWindow(Window window) => throw new NotSupportedException();
        public void ShowDialog(Window window) => OnShow(window);
    }

    /// <summary>Only one window is created at a time; closing disposes its VM once and permits reopening.</summary>
    [AvaloniaFact]
    public void OpenCloseOwnsExactlyOneDialog()
    {
        var logger = new TestLogger<SingleDialogWindowCoordinator>();
        var coordinator = new SingleDialogWindowCoordinator(new TestMainWindowService(), logger);
        var created = new List<TestViewModel>();
        TestViewModel Create() { var vm = new TestViewModel(); created.Add(vm); return vm; }
        try
        {
            coordinator.Open(Create, _ => new Window());
            coordinator.Open(Create, _ => throw new InvalidOperationException("duplicate must not create window"));
            Assert.Single(created);
            Assert.Equal(0, created[0].DisposeCount);
            coordinator.Close();
            Assert.Equal(1, created[0].DisposeCount);
            coordinator.Open(Create, _ => new Window());
            Assert.Equal(2, created.Count);
        }
        finally
        {
            coordinator.Close();
        }
        Assert.All(created, vm => Assert.Equal(1, vm.DisposeCount));
        Assert.Single(logger.Entries);
    }

    /// <summary>A ShowDialog exception is logged, disposes the VM and clears ownership so the next request is attempted.</summary>
    [AvaloniaFact]
    public void ShowFailureCleansUpAndAllowsRetry()
    {
        var failure = new InvalidOperationException("controlled show failure");
        var logger = new TestLogger<SingleDialogWindowCoordinator>();
        var coordinator = new SingleDialogWindowCoordinator(new TestMainWindowService { OnShow = _ => throw failure }, logger);
        var first = new TestViewModel();
        var second = new TestViewModel();
        coordinator.Open(() => first, _ => new Window());
        coordinator.Open(() => second, _ => new Window());
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Same(failure, entry.Exception));
    }

    /// <summary>A window factory failure releases its view model, preserves the exception and permits a successful retry.</summary>
    [AvaloniaFact]
    public void WindowFactoryFailureDisposesCreatedViewModel()
    {
        var vm = new TestViewModel();
        var failure = new InvalidOperationException("controlled factory failure");
        var coordinator = new SingleDialogWindowCoordinator(new TestMainWindowService(), new TestLogger<SingleDialogWindowCoordinator>());
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => coordinator.Open(() => vm, _ => throw failure)));
        Assert.Equal(1, vm.DisposeCount);
        var retry = new TestViewModel();
        try
        {
            coordinator.Open(() => retry, _ => new Window());
            Assert.Equal(0, retry.DisposeCount);
        }
        finally
        {
            coordinator.Close();
        }
        Assert.Equal(1, vm.DisposeCount);
        Assert.Equal(1, retry.DisposeCount);
    }

    /// <summary>A view model factory exception never invokes the window factory and does not block the next open.</summary>
    [AvaloniaFact]
    public void ViewModelFactoryFailurePreservesExceptionAndAllowsRetry()
    {
        var failure = new InvalidOperationException("controlled view model failure");
        var coordinator = new SingleDialogWindowCoordinator(new TestMainWindowService(), new TestLogger<SingleDialogWindowCoordinator>());
        var windowFactoryCalled = false;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => coordinator.Open<TestViewModel>(
            () => throw failure,
            _ => { windowFactoryCalled = true; return new Window(); })));
        Assert.False(windowFactoryCalled);
        var retry = new TestViewModel();
        try
        {
            coordinator.Open(() => retry, _ => new Window());
            Assert.Equal(0, retry.DisposeCount);
        }
        finally
        {
            coordinator.Close();
        }
        Assert.Equal(1, retry.DisposeCount);
    }

    /// <summary>A window closed synchronously before ShowDialog fails releases its view model only once.</summary>
    [AvaloniaFact]
    public void ShowFailureAfterSynchronousCloseDoesNotDisposeTwice()
    {
        var failure = new InvalidOperationException("show failed after closing");
        var logger = new TestLogger<SingleDialogWindowCoordinator>();
        var mainWindow = new TestMainWindowService
        {
            OnShow = window => { window.Show(); window.Close(); throw failure; }
        };
        var coordinator = new SingleDialogWindowCoordinator(mainWindow, logger);
        var vm = new TestViewModel();

        coordinator.Open(() => vm, _ => new Window());

        Assert.Equal(1, vm.DisposeCount);
        Assert.Same(failure, Assert.Single(logger.Entries).Exception);
        mainWindow.OnShow = window => window.Show();
        var retry = new TestViewModel();
        try
        {
            coordinator.Open(() => retry, _ => new Window());
        }
        finally
        {
            coordinator.Close();
        }
        Assert.Equal(1, retry.DisposeCount);
    }
}
