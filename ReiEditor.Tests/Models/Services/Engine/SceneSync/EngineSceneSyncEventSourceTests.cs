using System.Runtime.InteropServices;
using System.Text;
using ReiEditor.Models.Services.Engine.Api.DTO;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.SceneSync;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine.SceneSync;

[Trait("Area", "EntitySync")]
public sealed class EngineSceneSyncEventSourceTests
{
    private sealed class Api : TestEngineApi
    {
        public IEngineApi.SceneSyncCallbackDelegate? Callback { get; private set; }
        public int SnapshotRequests { get; private set; }
        public override void SetSceneSyncCallback(IEngineApi.SceneSyncCallbackDelegate? callback) => Callback = callback;
        public override void RequestSceneSync() => SnapshotRequests++;
    }

    [Fact]
    public void CallbackCopiesIdsAndUnsubscribesBeforeDllUnload()
    {
        var api = new Api();
        var source = new EngineSceneSyncEventSource(api, new TestLogger<EngineSceneSyncEventSource>());
        var received = new List<SceneSyncPacket>();
        source.Received += received.Add;
        source.Connect();
        Assert.True(source.IsConnected);
        Send(api.Callback!, """{"IsSnapshot":true,"Entities":[{"Id":7,"Name":"node","ParentId":0,"Order":2}],"SelectedEntityIds":[7,20]}""");
        Assert.Equal(new[] { 7, 20 }, Assert.Single(received).SelectedEntityIds);
        Assert.Equal(new SceneEntityState(7, "node", 0, 2), Assert.Single(received[0].Entities));
        source.Disconnect();
        Assert.Null(api.Callback);
        Assert.False(source.IsConnected);
        source.Connect();
        Send(api.Callback!, "{}");
        Assert.Empty(received[1].SelectedEntityIds);
        source.Disconnect();
    }

    [Fact]
    public void ExplicitSnapshotRequestRequiresConnectedEngine()
    {
        var api = new Api();
        var source = new EngineSceneSyncEventSource(api, new TestLogger<EngineSceneSyncEventSource>());
        source.RequestSnapshot();
        Assert.Equal(0, api.SnapshotRequests);
        source.Connect();
        source.RequestSnapshot();
        Assert.Equal(1, api.SnapshotRequests);
        source.Disconnect();
        source.RequestSnapshot();
        Assert.Equal(1, api.SnapshotRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptionsCannotCrossNativeBoundary(bool subscriberThrows)
    {
        var api = new Api();
        var logger = new TestLogger<EngineSceneSyncEventSource>();
        var source = new EngineSceneSyncEventSource(api, logger);
        source.Received += _ => throw new InvalidOperationException("subscriber failed");
        source.Connect();
        Send(api.Callback!, subscriberThrows ? "{}" : "{invalid");
        Assert.NotNull(Assert.Single(logger.Entries).Exception);
        source.Disconnect();
    }

    private static void Send(IEngineApi.SceneSyncCallbackDelegate callback, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try { Marshal.Copy(bytes, 0, pointer, bytes.Length); callback(pointer, bytes.Length); }
        finally { Marshal.FreeHGlobal(pointer); }
    }
}
