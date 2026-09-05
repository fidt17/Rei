using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine.Playmode;

/// <summary>Verifies playmode eligibility and engine mode routing without starting native execution.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Playmode")]
public sealed class PlaymodeStarterTests
{
    /// <summary>Build readiness and inactive playmode jointly control eligibility.</summary>
    [Fact]
    public void TestCanStartTracksBuildReadinessAndPlaymodeState()
    {
        var build = new TestBuildService();
        var engine = new TestEngineRunner();
        using var starter = new PlaymodeStarter(build, engine);

        Assert.False(starter.CanStart.IsTrue.Value);
        build.Ready.Value = true;
        Assert.True(starter.CanStart.IsTrue.Value);
        engine.PlaymodeActive.Value = true;
        Assert.False(starter.CanStart.IsTrue.Value);
        engine.PlaymodeActive.Value = false;
        Assert.True(starter.CanStart.IsTrue.Value);
    }

    /// <summary>Blocked starts do not call runner; eligible starts request PlayMode and return runner result.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestTryStartRoutesPlayModeAndReturnsRunnerResult(bool runnerResult)
    {
        var build = new TestBuildService();
        var engine = new TestEngineRunner();
        var modes = new List<EngineRunMode>();
        engine.OnStart = mode => { modes.Add(mode); return runnerResult; };
        using var starter = new PlaymodeStarter(build, engine);

        Assert.False(starter.TryStart());
        Assert.Empty(modes);
        build.Ready.Value = true;

        Assert.Equal(runnerResult, starter.TryStart());
        Assert.Equal(EngineRunMode.PlayMode, Assert.Single(modes));
    }

    /// <summary>Disposal detaches guard subscriptions and leaves its last eligibility snapshot unchanged.</summary>
    [Fact]
    public void TestDisposeDetachesEligibilitySubscriptions()
    {
        var build = new TestBuildService();
        var engine = new TestEngineRunner();
        build.Ready.Value = true;
        var starter = new PlaymodeStarter(build, engine);
        Assert.True(starter.CanStart.IsTrue.Value);

        starter.Dispose();
        engine.PlaymodeActive.Value = true;
        build.Ready.Value = false;

        Assert.True(starter.CanStart.IsTrue.Value);
    }
}
