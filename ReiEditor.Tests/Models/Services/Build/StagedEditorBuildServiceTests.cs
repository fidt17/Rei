using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies staged editor build routing, state changes and promotion ordering without native builds.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Build")]
public sealed class StagedEditorBuildServiceTests
{
    private sealed class TestBuildStateService(List<string> calls) : IProjectBuildStateService
    {
        public ProjectBuildState State { get; set; } = new(true, true, "test");
        public Func<Task>? OnSave { get; set; }
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext Context, bool Solution, bool Assets)> Calculations { get; } = new();
        public List<(BuildConfigurationEnum Configuration, BuildExecutionContext Context, bool Solution, bool Assets)> Saves { get; } = new();

        public Task<ProjectBuildState> CalculateState(BuildConfigurationEnum configuration, BuildExecutionContext buildContext, bool buildSolution, bool buildAssets)
        {
            calls.Add("calculate");
            Calculations.Add((configuration, buildContext, buildSolution, buildAssets));
            return Task.FromResult(State);
        }

        public void MarkBuildStarted(BuildConfigurationEnum configuration, BuildExecutionContext buildContext) => calls.Add("started");
        public void MarkBuildFailed(BuildConfigurationEnum configuration, BuildExecutionContext buildContext) => calls.Add("failed");

        public async Task SaveSuccessfulBuild(BuildConfigurationEnum configuration, BuildExecutionContext buildContext, bool buildSolution, bool buildAssets)
        {
            calls.Add("save");
            Saves.Add((configuration, buildContext, buildSolution, buildAssets));
            if (OnSave != null) await OnSave();
        }
    }

    private sealed class TestEngineBuildGate(List<string> calls) : IEngineBuildGate
    {
        public Func<CancellationToken, Task> OnStop { get; set; } = _ => Task.CompletedTask;

        public Task StopEngineAndWaitForDllUnload(CancellationToken cancellationToken)
        {
            calls.Add("stop");
            return OnStop(cancellationToken);
        }
    }

    private sealed class TestOutputService(TemporaryDirectory directory, List<string> calls) : IEditorBuildOutputService
    {
        public EditorBuildOutput Live { get; } = CreateOutput(directory.GetPath("live"));
        public EditorBuildOutput Staged { get; } = CreateOutput(directory.GetPath("staging"));
        public Action? OnPromote { get; set; }

        public EditorBuildOutput GetLiveOutput() { calls.Add("live"); return Live; }
        public EditorBuildOutput PrepareStagingOutput() { calls.Add("prepare"); return Staged; }
        public void SeedStagingClientOutputFromLive(EditorBuildOutput stagingOutput) { Assert.Same(Staged, stagingOutput); calls.Add("seed"); }
        public void PromoteStagingOutput(EditorBuildOutput stagingOutput) { Assert.Same(Staged, stagingOutput); calls.Add("promote"); OnPromote?.Invoke(); }
        public void CleanupStagingOutput() => calls.Add("cleanup");

        private static EditorBuildOutput CreateOutput(string root) => new(
            root,
            Path.Combine(root, "bin"),
            Path.Combine(root, "client"),
            Path.Combine(root, "client", "Client.dll"),
            Path.Combine(root, "bin", "Resources"));
    }

    private sealed class TestContext : IDisposable
    {
        public TemporaryDirectory Directory { get; } = new();
        public List<string> Calls { get; } = new();
        public TestBuildService Build { get; } = new();
        public TestEngineRunner Runner { get; } = new();
        public TestBuildStateService State { get; }
        public TestEngineBuildGate Gate { get; }
        public TestOutputService Output { get; }
        public StagedEditorBuildService Service { get; }

        public TestContext()
        {
            State = new(Calls);
            Gate = new(Calls);
            Output = new(Directory, Calls);
            Service = new(Build, State, Runner, Gate, Output);
        }

        public void Dispose() => Directory.Dispose();
    }

    /// <summary>Only an active non-playmode editor debug build without a custom context uses staging.</summary>
    [Theory]
    [InlineData(BuildConfigurationEnum.EditorDebug, true, false, false, true)]
    [InlineData(BuildConfigurationEnum.Debug, true, false, false, false)]
    [InlineData(BuildConfigurationEnum.EditorDebug, false, false, false, false)]
    [InlineData(BuildConfigurationEnum.EditorDebug, true, true, false, false)]
    [InlineData(BuildConfigurationEnum.EditorDebug, true, false, true, false)]
    public void EligibilityRequiresActiveEditorDebugWithoutPlaymodeOrContext(BuildConfigurationEnum configuration, bool editorActive, bool playmodeActive, bool customContext, bool expected)
    {
        using var context = new TestContext();
        context.Runner.EditorActive.Value = editorActive;
        context.Runner.PlaymodeActive.Value = playmodeActive;
        var buildContext = customContext ? new BuildExecutionContext(context.Directory.GetPath("custom")) : null;

        Assert.Equal(expected, context.Service.ShouldUseStagedEditorBuild(configuration, buildContext));
    }

    /// <summary>A current live state returns success before staging or touching engine state.</summary>
    [Fact]
    public async Task CurrentBuildReturnsWithoutPreparingStaging()
    {
        using var context = new TestContext();
        context.State.State = new(false, false, "current");

        var result = await context.Service.BuildAndPromote(BuildConfigurationEnum.EditorDebug, false, false, false, true, true, null, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(new[] { "live", "calculate" }, context.Calls);
        Assert.Empty(context.Build.Requests);
    }

    /// <summary>An assets-only build seeds client output, uses live cache, forwards progress and promotes before persisting.</summary>
    [Fact]
    public async Task AssetsOnlySuccessSeedsAndPromotesBeforeSaving()
    {
        using var context = new TestContext();
        context.State.State = new(false, true, "assets");
        AssetBuildProgressInfo? forwarded = null;
        context.Build.OnBuild = request =>
        {
            context.Calls.Add("build");
            request.Progress!(new AssetBuildProgressInfo(2, 3, "Project/texture.png"));
            return Task.FromResult(true);
        };
        context.Gate.OnStop = _ => { Assert.Equal("build", context.Calls[^2]); return Task.CompletedTask; };

        var result = await context.Service.BuildAndPromote(BuildConfigurationEnum.EditorDebug, false, true, false, true, true, p => forwarded = p, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(new[] { "live", "calculate", "prepare", "seed", "started", "build", "stop", "promote", "save", "cleanup" }, context.Calls);
        var request = Assert.Single(context.Build.Requests);
        Assert.True(request.ForceSolution);
        Assert.True(request.CleanSolution);
        Assert.False(request.ForceAssets);
        Assert.False(request.BuildSolution);
        Assert.True(request.BuildAssets);
        Assert.Equal(context.Output.Staged.BinDirectoryPath, request.Context!.BuildFolder);
        Assert.Equal(Path.Combine(context.Output.Live.ResourcesDirectoryPath, "Cache"), request.Context.AssetCacheDirectoryPath);
        Assert.Equal(new AssetBuildProgressInfo(2, 3, "Project/texture.png"), forwarded);
        var save = Assert.Single(context.State.Saves);
        Assert.False(save.Solution);
        Assert.True(save.Assets);
        Assert.Equal(context.Output.Live.BinDirectoryPath, save.Context.BuildFolder);
    }

    /// <summary>A false build marks live state failed, skips engine and promotion, then cleans staging.</summary>
    [Fact]
    public async Task FailedBuildMarksFailedAndCleansUp()
    {
        using var context = new TestContext();
        context.Build.OnBuild = _ => { context.Calls.Add("build"); return Task.FromResult(false); };

        var result = await context.Service.BuildAndPromote(BuildConfigurationEnum.EditorDebug, false, false, false, true, true, null, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(new[] { "live", "calculate", "prepare", "started", "build", "failed", "cleanup" }, context.Calls);
        Assert.Empty(context.State.Saves);
    }

    /// <summary>Gate cancellation marks failure, skips promotion and persistence, and always cleans staging.</summary>
    [Fact]
    public async Task GateCancellationMarksFailedAndCleansUp()
    {
        using var context = new TestContext();
        context.Build.OnBuild = _ => { context.Calls.Add("build"); return Task.FromResult(true); };
        context.Gate.OnStop = _ => Task.FromCanceled(new CancellationToken(canceled: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Service.BuildAndPromote(
            BuildConfigurationEnum.EditorDebug, false, false, false, true, true, null, CancellationToken.None));

        Assert.Equal(new[] { "live", "calculate", "prepare", "started", "build", "stop", "failed", "cleanup" }, context.Calls);
        Assert.Empty(context.State.Saves);
    }

    /// <summary>Promotion or persistence errors mark failure and preserve cleanup ordering.</summary>
    [Theory]
    [InlineData("promote")]
    [InlineData("save")]
    public async Task PostBuildFailureMarksFailedAndCleansUp(string stage)
    {
        using var context = new TestContext();
        var failure = new IOException("controlled " + stage + " failure");
        context.Build.OnBuild = _ => { context.Calls.Add("build"); return Task.FromResult(true); };
        if (stage == "promote") context.Output.OnPromote = () => throw failure;
        else context.State.OnSave = () => Task.FromException(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => context.Service.BuildAndPromote(
            BuildConfigurationEnum.EditorDebug, true, false, true, true, true, null, CancellationToken.None)));

        var expected = stage == "promote"
            ? new[] { "live", "calculate", "prepare", "started", "build", "stop", "promote", "failed", "cleanup" }
            : new[] { "live", "calculate", "prepare", "started", "build", "stop", "promote", "save", "failed", "cleanup" };
        Assert.Equal(expected, context.Calls);
    }
}
