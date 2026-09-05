using System.Numerics;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Scenes;

namespace ReiEditor.Tests.Models.Services.Scenes;

/// <summary>Verifies drop orchestration, paired inputs, asynchronous completion and failure propagation.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "SceneAssetDrop")]
public sealed class SceneAssetDropServiceTests
{
    private sealed class TestTargets : ISceneAssetDropTargetBuilderService
    {
        public IReadOnlyList<SceneAssetDropTarget> Targets { get; set; } = Array.Empty<SceneAssetDropTarget>();
        public IReadOnlyList<string>? Received { get; private set; }
        public bool CanHandleAssetPaths(IReadOnlyList<string> assetPaths) { Received = assetPaths; return Targets.Count > 0; }
        public IReadOnlyList<SceneAssetDropTarget> BuildTargets(IReadOnlyList<string> assetPaths) { Received = assetPaths; return Targets; }
    }

    private sealed class TestPlacements : ISceneAssetPlacementService
    {
        public Func<IReadOnlyList<SceneAssetDropTarget>, IReadOnlyList<SceneAssetDropPlacement>>? OnBuild { get; set; }
        public IReadOnlyList<SceneAssetDropPlacement> BuildPlacements(IReadOnlyList<SceneAssetDropTarget> targets) => (OnBuild ?? throw new NotSupportedException())(targets);
    }

    private sealed class TestInitialization : ISceneAssetEntityInitializationService
    {
        public Func<SceneAssetDropTarget, SceneAssetDropPlacement, Task<bool>>? OnCreate { get; set; }
        public Task<bool> CreateEntityForAsset(SceneAssetDropTarget target, SceneAssetDropPlacement placement) => (OnCreate ?? throw new NotSupportedException())(target, placement);
    }

    /// <summary>An unsupported batch exits without invoking placement or entity creation.</summary>
    [Fact]
    public async Task EmptyTargetsAvoidDownstreamCalls()
    {
        var targets = new TestTargets();
        var service = new SceneAssetDropService(targets, new TestPlacements(), new TestInitialization());
        var paths = new[] { "unknown" };
        Assert.False(service.CanHandleAssetPaths(paths));
        Assert.Same(paths, targets.Received);
        Assert.Equal(0, await service.CreateEntitiesFromAssets(paths));
        Assert.Same(paths, targets.Received);
    }

    /// <summary>A batch starts every creation, waits for all results and counts only successful entities.</summary>
    [Fact]
    public async Task BatchWaitsForAllResultsAndKeepsTargetPlacementPairs()
    {
        var targets = new TestTargets { Targets = new[] { Target("a"), Target("b") } };
        var placements = new[] { new SceneAssetDropPlacement(Vector3.UnitX, Vector3.Zero), new SceneAssetDropPlacement(Vector3.UnitY, Vector3.One) };
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<(SceneAssetDropTarget, SceneAssetDropPlacement)>();
        var service = new SceneAssetDropService(targets, new TestPlacements { OnBuild = batch => { Assert.Same(targets.Targets, batch); return placements; } },
            new TestInitialization { OnCreate = (target, placement) => { received.Add((target, placement)); return target.AssetId == "a" ? first.Task : second.Task; } });
        var pending = service.CreateEntitiesFromAssets(new[] { "a", "b" });
        try
        {
            Assert.Equal(new[] { (targets.Targets[0], placements[0]), (targets.Targets[1], placements[1]) }, received);
            Assert.False(pending.IsCompleted);
            second.SetResult(true);
            Assert.False(pending.IsCompleted);
            first.SetResult(false);
            Assert.Equal(1, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            first.TrySetResult(false);
            second.TrySetResult(false);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>An initializer fault propagates to the caller instead of becoming a successful creation count.</summary>
    [Fact]
    public async Task InitializerFailurePropagates()
    {
        var failure = new InvalidOperationException("creation failed");
        var service = new SceneAssetDropService(new TestTargets { Targets = new[] { Target("a") } },
            new TestPlacements { OnBuild = _ => new[] { new SceneAssetDropPlacement(Vector3.Zero, Vector3.Zero) } },
            new TestInitialization { OnCreate = (_, _) => Task.FromException<bool>(failure) });
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateEntitiesFromAssets(new[] { "a" })));
    }

    private static SceneAssetDropTarget Target(string id) => new(id, AssetType.Model, id, id);
}
