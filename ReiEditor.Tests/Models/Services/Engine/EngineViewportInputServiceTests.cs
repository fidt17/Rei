using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Input;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine;

public sealed class EngineViewportInputServiceTests
{
    private sealed class Native : TestEngineApi, IEngineApi, IEngineNativeAccess
    {
        public bool Available = true;
        public bool Supported = true;
        public int Result = 1;
        public int Calls;
        public bool HasExport(string name) => Supported;
        public object? EditorActionContext { get; } = new object();

        public bool TryInvoke(Action<IEngineApi> action) { if (!Available) return false; action(this); return true; }
        public override T Invoke<T>(Type delegateType, string methodName, params object?[]? args) { Calls++; return (T)(object)Result; }
    }

    [Theory]
    [InlineData("unknown", 0, 0, 0)]
    [InlineData("move", -1, 0, 0)]
    [InlineData("move", double.NaN, 0, 0)]
    [InlineData("button_down", 0, 0, 3)]
    [InlineData("key_down", 0, 0, -1)]
    public async Task InvalidInputNeverReachesNative(string type, double x, double y, int code)
    {
        var native = new Native();
        var error = await Assert.ThrowsAsync<ReiMcpOperationException>(() => new EngineViewportInputService(native).SendAsync([new(type, x, y, code)], default));
        Assert.Equal("invalid_input", error.Code);
        Assert.Equal(0, native.Calls);
    }

    [Theory]
    [InlineData(-1, "input_unavailable")]
    [InlineData(-2, "input_busy")]
    [InlineData(-3, "invalid_input")]
    [InlineData(-4, "input_timeout")]
    public async Task NativeFailureIsNotReportedAsSuccessfulInput(int result, string code)
    {
        var native = new Native { Result = result };
        var error = await Assert.ThrowsAsync<ReiMcpOperationException>(() => new EngineViewportInputService(native).DragAsync(1, 2, 3, 4, default));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task UnavailableEngineUnsupportedDllAndCancellationAreExplicit()
    {
        var native = new Native { Available = false };
        var service = new EngineViewportInputService(native);
        Assert.Equal("engine_unavailable", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => service.DragAsync(1, 2, 3, 4, default))).Code);
        native.Available = true;
        native.Supported = false;
        Assert.Equal("input_unsupported", (await Assert.ThrowsAsync<ReiMcpOperationException>(() => service.DragAsync(1, 2, 3, 4, default))).Code);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DragAsync(1, 2, 3, 4, new CancellationToken(true)));
        Assert.Equal(0, native.Calls);
    }
}
