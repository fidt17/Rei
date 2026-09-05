using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies build command guards, staged routing, argument forwarding and reentry recovery.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class BuildStarterTests
{
    private sealed class TestStagedBuildService : IStagedEditorBuildService
    {
        public bool UseStaging { get; set; }
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext? Context)> Eligibility { get; } = new();
        public List<TestBuildRequest> Requests { get; } = new();
        public Func<TestBuildRequest, Task<bool>>? OnBuild { get; set; }
        public bool ShouldUseStagedEditorBuild(BuildConfigurationEnum configuration, BuildExecutionContext? context)
        {
            Eligibility.Add((configuration, context));
            return UseStaging;
        }
        public Task<bool> BuildAndPromote(BuildConfigurationEnum configuration, bool forceSolutionRebuild, bool forceCleanSolutionBuild, bool forceAssetRebuild, bool buildSolution, bool buildAssets, Action<AssetBuildProgressInfo>? onAssetBuilding, CancellationToken cancellationToken)
        {
            var request = new TestBuildRequest(configuration, forceSolutionRebuild, forceCleanSolutionBuild, forceAssetRebuild, null, buildSolution, buildAssets, onAssetBuilding, cancellationToken);
            Requests.Add(request);
            return (OnBuild ?? throw new NotSupportedException())(request);
        }
    }

    private sealed class TestContext : IDisposable
    {
        public TestBuildService Build { get; } = new();
        public TestAssetsService Assets { get; } = new();
        public TestEngineRunner Engine { get; } = new();
        public TestAssetImporter Importer { get; } = new();
        public TestStagedBuildService Staged { get; } = new();
        public TestLogger<BuildStarter> Logger { get; } = new();
        public BuildStarter Service { get; }
        public TestContext() => Service = new(Build, Assets, Engine, Staged, Importer, Logger);
        public void Dispose() => Service.Dispose();
    }

    /// <summary>Chosen route receives all build options, callbacks and cancellation token and returns its result.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoutesAndForwardsAllArguments(bool staged)
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        var execution = staged ? null : new BuildExecutionContext("controlled-output");
        Action<AssetBuildProgressInfo> progress = _ => { };
        context.Staged.UseStaging = staged;
        context.Build.OnBuild = _ => Task.FromResult(false);
        context.Staged.OnBuild = _ => Task.FromResult(false);

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorRelease, true, true, true, execution, false, true, progress, cancellation.Token));

        Assert.Equal((BuildConfigurationEnum.EditorRelease, execution), Assert.Single(context.Staged.Eligibility));
        var request = Assert.Single(staged ? context.Staged.Requests : context.Build.Requests);
        Assert.Equal(BuildConfigurationEnum.EditorRelease, request.Configuration);
        Assert.True(request.ForceSolution && request.CleanSolution && request.ForceAssets);
        Assert.Same(execution, request.Context);
        Assert.False(request.BuildSolution);
        Assert.True(request.BuildAssets);
        Assert.Same(progress, request.Progress);
        Assert.Equal(cancellation.Token, request.Cancellation);
        Assert.Empty(staged ? context.Build.Requests : context.Staged.Requests);
    }

    /// <summary>Build, playmode, startup, saving and importing each independently block the command until cleared.</summary>
    [Theory]
    [InlineData("build")]
    [InlineData("playmode")]
    [InlineData("starting")]
    [InlineData("saving")]
    [InlineData("importing")]
    public async Task BusyDependencyBlocksBuildAndClearingItRestoresEligibility(string busy)
    {
        using var context = new TestContext();
        var state = busy switch
        {
            "build" => context.Build.InProgress,
            "playmode" => context.Engine.PlaymodeActive,
            "starting" => context.Engine.Starting,
            "saving" => context.Assets.Saving,
            _ => context.Importer.Importing
        };
        state.Value = true;

        Assert.False(context.Service.CanStartBuild.IsTrue.Value);
        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
        Assert.Empty(context.Staged.Eligibility);
        Assert.Empty(context.Build.Requests);

        state.Value = false;
        context.Build.OnBuild = _ => Task.FromResult(true);
        Assert.True(context.Service.CanStartBuild.IsTrue.Value);
        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
    }

    /// <summary>Interlocked command guard rejects reentry even while dependency observables remain idle.</summary>
    [Fact]
    public async Task RejectsReentryBeforeDependencyMarksBuildActive()
    {
        using var context = new TestContext();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Build.OnBuild = _ => gate.Task;
        var first = context.Service.BuildProject(BuildConfigurationEnum.EditorDebug);
        try
        {
            Assert.False(first.IsCompleted);
            Assert.True(context.Service.CanStartBuild.IsTrue.Value);
            Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
            Assert.Single(context.Build.Requests);
            gate.SetResult(true);
            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
            Assert.Equal(2, context.Build.Requests.Count);
        }
        finally
        {
            gate.TrySetResult(true);
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Exceptions and cancellation from either route return false and release the command guard for retry.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RouteFailureAllowsRetry(bool staged, bool canceled)
    {
        using var context = new TestContext();
        context.Staged.UseStaging = staged;
        Exception failure = canceled ? new OperationCanceledException() : new IOException("controlled route failure");
        context.Build.OnBuild = _ => Task.FromException<bool>(failure);
        context.Staged.OnBuild = _ => Task.FromException<bool>(failure);

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));

        if (!canceled) Assert.Contains(context.Logger.Entries, entry => ReferenceEquals(entry.Exception, failure));
        context.Build.OnBuild = _ => Task.FromResult(true);
        context.Staged.OnBuild = _ => Task.FromResult(true);
        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
    }

    /// <summary>A pre-canceled command never evaluates staging or invokes either build route.</summary>
    [Fact]
    public async Task PreCanceledCommandDoesNotStartBuild()
    {
        using var context = new TestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.False(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug, cancellationToken: cancellation.Token));

        Assert.Empty(context.Staged.Eligibility);
        Assert.Empty(context.Build.Requests);
        context.Build.OnBuild = _ => Task.FromResult(true);
        Assert.True(await context.Service.BuildProject(BuildConfigurationEnum.EditorDebug));
    }

    /// <summary>Disposal detaches guard subscriptions so later engine/build changes do not update the disposed condition.</summary>
    [Fact]
    public void DisposeDetachesConditionSubscriptions()
    {
        var context = new TestContext();
        Assert.True(context.Service.CanStartBuild.IsTrue.Value);
        context.Dispose();

        context.Build.InProgress.Value = true;

        Assert.True(context.Service.CanStartBuild.IsTrue.Value);
    }
}
