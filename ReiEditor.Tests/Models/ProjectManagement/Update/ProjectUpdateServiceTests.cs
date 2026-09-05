using ReiEditor.Models.ProjectManagement.Update;
using ReiEditor.Models.Resources.EngineResources;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement.Update;

/// <summary>Verifies project update ordering and failure boundaries without running import or build tools.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Projects")]
public sealed class ProjectUpdateServiceTests
{
    private sealed class TestEngineResourcesImporter(Func<Task> import) : IEngineResourcesImporter
    {
        public Task Import() => import();
    }

    private sealed class TestAssetImporter(Func<Task<List<AssetInfo>>> import) : IAssetImporter
    {
        public event Action ImportedAssetsEvent { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportAll() => import();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
    }

    /// <summary>Each asynchronous update stage finishes before the next stage starts.</summary>
    [Fact]
    public async Task AwaitsSolutionThenEngineResourcesThenAssetImport()
    {
        using var fixture = new TemporaryProjectFixture();
        fixture.Project.SetProjectVisualStudioProjectPath(fixture.Directory.GetPath("Game.vcxproj"));
        var stages = new List<string>();
        var solutionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resourcesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resourcesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generator = new TestSolutionGenerator
        {
            OnUpdate = path =>
            {
                Assert.Equal(fixture.Project.ProjectVisualStudioProjectPath, path);
                stages.Add("solution");
                return solutionGate.Task;
            }
        };
        var service = new ProjectUpdateService(generator, new TestEngineResourcesImporter(() =>
        {
            stages.Add("resources");
            resourcesStarted.SetResult();
            return resourcesGate.Task;
        }), new TestAssetImporter(() =>
        {
            stages.Add("assets");
            return Task.FromResult(new List<AssetInfo>());
        }));

        var update = service.UpdateProject(fixture.Project);
        try
        {
            Assert.False(update.IsCompleted);
            Assert.Equal(new[] { "solution" }, stages);
            solutionGate.SetResult();
            await resourcesStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "solution", "resources" }, stages);
            Assert.False(update.IsCompleted);
            resourcesGate.SetResult();
            await update.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "solution", "resources", "assets" }, stages);
        }
        finally
        {
            solutionGate.TrySetResult();
            resourcesGate.TrySetResult();
            await update.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>A failed update stage propagates its exception and prevents subsequent stages.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StopsAtFailedStage(int failureStage)
    {
        using var fixture = new TemporaryProjectFixture();
        var calls = new List<int>();
        var failure = new IOException("controlled update failure");
        Task Stage(int stage)
        {
            calls.Add(stage);
            return stage == failureStage ? Task.FromException(failure) : Task.CompletedTask;
        }
        var service = new ProjectUpdateService(
            new TestSolutionGenerator { OnUpdate = _ => Stage(0) },
            new TestEngineResourcesImporter(() => Stage(1)),
            new TestAssetImporter(async () => { await Stage(2); return new List<AssetInfo>(); }));

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.UpdateProject(fixture.Project)));
        Assert.Equal(Enumerable.Range(0, failureStage + 1), calls);
    }
}
