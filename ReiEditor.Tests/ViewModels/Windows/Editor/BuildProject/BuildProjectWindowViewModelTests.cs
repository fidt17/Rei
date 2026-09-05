using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.ProjectBuildWindow;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.Active;
using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.ProjectBuild;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.BuildProject;
using ProjectModel = ReiEditor.Models.ProjectManagement.Project;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.BuildProject;

/// <summary>Verifies build form state, progress, outcomes, and cancellation using a controlled build.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "ViewModels")]
public sealed class BuildProjectWindowViewModelTests : IDisposable
{
    /// <summary>Provides fixed active project for default output path calculation.</summary>
    private sealed class TestActiveProjectService(ProjectModel project) : IActiveProjectService
    {
        public event Action<ProjectModel>? ActiveProjectChangedEvent;
        public ProjectModel GetActiveProject() => project;
        public void OpenProject(ProjectModel value) => ActiveProjectChangedEvent?.Invoke(value);
    }

    /// <summary>Records window close requests.</summary>
    private sealed class TestProjectBuildWindowService : IProjectBuildWindowService
    {
        public ReiEditor.Utils.Common.IObservable<bool> IsOpened { get; } = new ReiEditor.Utils.Common.Observable<bool>(false);
        public int CloseCount { get; private set; }
        public void OpenWindow() { }
        public void CloseWindow() => CloseCount++;
    }

    /// <summary>Holds a build until test selects success, failure, or cancellation.</summary>
    private sealed class TestProjectBuildService : IProjectBuildService
    {
        private readonly TaskCompletionSource<ProjectBuildResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProjectBuildRequest Request { get; private set; }
        public Action<ProjectBuildProgress>? ProgressCallback { get; private set; }
        public int CancellationCount { get; private set; }
        public Task Started => _started.Task;

        public Task<ProjectBuildResult> BuildAsync(ProjectBuildRequest request, Action<ProjectBuildProgress> progressCallback, CancellationToken cancellationToken)
        {
            Request = request;
            ProgressCallback = progressCallback;
            cancellationToken.Register(() =>
            {
                CancellationCount++;
                _completion.TrySetCanceled(cancellationToken);
            });
            _started.TrySetResult();
            return _completion.Task;
        }

        public void Complete(ProjectBuildResult result) => _completion.TrySetResult(result);
        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }

    private readonly TemporaryProjectFixture _project = new();

    /// <summary>Build command locks while running, applies progress, and closes window on success.</summary>
    [AvaloniaFact]
    public async Task TestSuccessfulBuildTracksRequestProgressAndCommandAvailability()
    {
        var build = new TestProjectBuildService();
        var window = new TestProjectBuildWindowService();
        using var vm = TestCreateViewModel(build, window);
        vm.SelectedConfiguration = BuildConfigurationEnum.Debug;
        vm.ShowConsole = false;
        vm.IconPath = "game.ico";

        vm.BuildCommand.Execute(null);
        try
        {
            await build.Started.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.True(vm.IsBuildInProgress);
            Assert.False(vm.BuildCommand.CanExecute(null));
            Assert.Equal(BuildConfigurationEnum.Debug, build.Request.Configuration);
            Assert.Equal(vm.OutputPath, build.Request.OutputPath);
            Assert.False(build.Request.ShowConsole);
            Assert.Equal("game.ico", build.Request.IconPath);

            build.ProgressCallback?.Invoke(new ProjectBuildProgress("Packaging", 2, 4));
            await Dispatcher.UIThread.InvokeAsync(() => { });
            Assert.Equal("Packaging", vm.ProgressStatus);
            Assert.Equal(0.5, vm.ProgressValue);

            var idle = TestWaitForIdleAsync(vm);
            build.Complete(new ProjectBuildResult(true, false, string.Empty));
            await idle;

            Assert.True(vm.BuildCommand.CanExecute(null));
            Assert.Equal(1, window.CloseCount);
            Assert.Empty(vm.ErrorText);
        }
        finally
        {
            await TestFinishBuildAsync(build, vm);
        }
    }

    /// <summary>Failed result restores idle state, keeps window open, and exposes service error.</summary>
    [AvaloniaFact]
    public async Task TestFailedBuildRestoresIdleStateAndDisplaysError()
    {
        var build = new TestProjectBuildService();
        var window = new TestProjectBuildWindowService();
        using var vm = TestCreateViewModel(build, window);
        vm.BuildCommand.Execute(null);
        try
        {
            await build.Started.WaitAsync(TimeSpan.FromSeconds(3));

            var idle = TestWaitForIdleAsync(vm);
            build.Complete(new ProjectBuildResult(false, false, "Link failed"));
            await idle;

            Assert.Equal("Link failed", vm.ErrorText);
            Assert.Equal("Build failed", vm.ProgressStatus);
            Assert.False(vm.IsCancelRequested);
            Assert.Equal(0, window.CloseCount);
        }
        finally
        {
            await TestFinishBuildAsync(build, vm);
        }
    }

    /// <summary>Thrown build exception restores idle state and exposes exception message.</summary>
    [AvaloniaFact]
    public async Task TestBuildExceptionRestoresIdleStateAndDisplaysMessage()
    {
        var build = new TestProjectBuildService();
        var window = new TestProjectBuildWindowService();
        using var vm = TestCreateViewModel(build, window);
        vm.BuildCommand.Execute(null);
        try
        {
            await build.Started.WaitAsync(TimeSpan.FromSeconds(3));

            var idle = TestWaitForIdleAsync(vm);
            build.Fail(new InvalidOperationException("Build service unavailable"));
            await idle;

            Assert.Equal("Build service unavailable", vm.ErrorText);
            Assert.Equal("Build failed", vm.ProgressStatus);
            Assert.True(vm.BuildCommand.CanExecute(null));
            Assert.Equal(0, window.CloseCount);
        }
        finally
        {
            await TestFinishBuildAsync(build, vm);
        }
    }

    /// <summary>Repeated cancel requests signal one token cancellation and finish in canceled state.</summary>
    [AvaloniaFact]
    public async Task TestCancelSignalsBuildOnceAndRestoresIdleState()
    {
        var build = new TestProjectBuildService();
        var window = new TestProjectBuildWindowService();
        using var vm = TestCreateViewModel(build, window);
        vm.BuildCommand.Execute(null);
        try
        {
            await build.Started.WaitAsync(TimeSpan.FromSeconds(3));

            var idle = TestWaitForIdleAsync(vm);
            vm.CancelCommand.Execute(null);
            vm.CancelCommand.Execute(null);
            await idle;

            Assert.Equal(1, build.CancellationCount);
            Assert.False(vm.IsCancelRequested);
            Assert.Equal("Build canceled", vm.ProgressStatus);
            Assert.Equal("Build canceled.", vm.ErrorText);
            Assert.Equal(0, window.CloseCount);
        }
        finally
        {
            await TestFinishBuildAsync(build, vm);
        }
    }

    /// <summary>Idle cancel acts as close without starting a build.</summary>
    [AvaloniaFact]
    public void TestCancelWhileIdleClosesWindow()
    {
        var build = new TestProjectBuildService();
        var window = new TestProjectBuildWindowService();
        using var vm = TestCreateViewModel(build, window);

        vm.CancelCommand.Execute(null);

        Assert.Equal(1, window.CloseCount);
        Assert.False(build.Started.IsCompleted);
    }

    /// <summary>Creates build form with isolated project paths and no platform picker use.</summary>
    private BuildProjectWindowViewModel TestCreateViewModel(TestProjectBuildService build, TestProjectBuildWindowService window)
    {
        var outputPaths = new ProjectBuildOutputPathUtility(new TestActiveProjectService(_project.Project), _project.Resources);
        return new BuildProjectWindowViewModel(build, window, outputPaths, null!);
    }

    /// <summary>Waits for build VM to publish terminal idle state without timing polls.</summary>
    private static async Task TestWaitForIdleAsync(BuildProjectWindowViewModel vm)
    {
        if (!vm.IsBuildInProgress) return;
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void HandlePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(BuildProjectWindowViewModel.IsBuildInProgress) && !vm.IsBuildInProgress)
            {
                idle.TrySetResult();
            }
        }

        vm.PropertyChanged += HandlePropertyChanged;
        try
        {
            if (!vm.IsBuildInProgress) return;
            await idle.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            vm.PropertyChanged -= HandlePropertyChanged;
        }
    }

    /// <summary>Releases a controlled build and waits until async-void command cleanup completes.</summary>
    private static async Task TestFinishBuildAsync(TestProjectBuildService build, BuildProjectWindowViewModel vm)
    {
        build.Complete(new ProjectBuildResult(false, true, "Test cleanup"));
        if (vm.IsBuildInProgress) await TestWaitForIdleAsync(vm);
    }

    /// <summary>Deletes isolated project directory.</summary>
    public void Dispose() => _project.Dispose();
}
