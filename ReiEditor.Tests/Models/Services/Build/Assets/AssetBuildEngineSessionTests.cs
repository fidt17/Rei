using ReiEditor.Models.Services.Build.Assets;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build.Assets;

/// <summary>Verifies engine session ownership and disposal callback behavior.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class AssetBuildEngineSessionTests
{
    /// <summary>Provides non-null engine API identity without loading native engine code.</summary>
    private sealed class TestEngineApi : EngineApi
    {
        public TestEngineApi() : base(new TestLogger<EngineApi>())
        {
        }
    }

    /// <summary>Session exposes supplied API instance without invoking disposal callback early.</summary>
    [Fact]
    public void TestConstructorExposesEngineApi()
    {
        var calls = 0;
        var engineApi = new TestEngineApi();
        using var session = new AssetBuildEngineSession(engineApi, () => calls++);

        Assert.Same(engineApi, session.EngineApi);
        Assert.Equal(0, calls);
    }

    /// <summary>Dispose invokes optional ownership callback.</summary>
    [Fact]
    public void TestDisposeInvokesCallback()
    {
        var calls = 0;
        var session = new AssetBuildEngineSession(new TestEngineApi(), () => calls++);

        session.Dispose();

        Assert.Equal(1, calls);
    }

    /// <summary>Session without ownership callback disposes safely.</summary>
    [Fact]
    public void TestDisposeAllowsMissingCallback()
    {
        var session = new AssetBuildEngineSession(new TestEngineApi());

        session.Dispose();
    }
}
