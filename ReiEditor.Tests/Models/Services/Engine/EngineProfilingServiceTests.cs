using System.Text;
using System.Text.Json;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine;

public sealed class EngineProfilingServiceTests
{
    private sealed class Api : TestEngineApi, IEngineApi
    {
        public bool Supported { get; set; } = true;
        public string Response { get; set; } = "{\"source\":\"runtime\",\"status\":\"ok\",\"captureId\":\"8\"}";
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }
        public bool HasExport(string name) => Supported;
        public override T Invoke<T>(Type delegateType, string methodName, params object?[]? args)
        {
            Method = methodName;
            Arguments = args;
            var buffer = Assert.IsType<byte[]>(args![^2]);
            var bytes = Encoding.UTF8.GetBytes(Response + '\0');
            Array.Copy(bytes, buffer, bytes.Length);
            return (T)(object)bytes.Length;
        }
    }

    private sealed class Access(Api api) : IEngineNativeAccess
    {
        public bool Available { get; set; } = true;
        public int Calls { get; private set; }
        public bool TryInvoke(Action<IEngineApi> action)
        {
            if (!Available) return false;
            Calls++;
            action(api);
            return true;
        }
    }

    [Fact]
    public void ReadAndCaptureUseDifferentNativeExportsWithExactArguments()
    {
        var api = new Api();
        var access = new Access(api);
        var service = new EngineProfilingService(access);
        Assert.Equal("ok", service.Read("runtime", "last_capture", "123", 17).GetProperty("status").GetString());
        Assert.Equal("GetProfilingSnapshot", api.Method);
        Assert.Equal("last_capture", api.Arguments![0]);
        Assert.Equal("123", api.Arguments[1]);
        Assert.Equal(17, api.Arguments[2]);
        service.StartCapture(240);
        Assert.Equal("StartProfilingCapture", api.Method);
        Assert.Equal(240, api.Arguments![0]);
        Assert.Equal(2, access.Calls);
    }

    [Theory]
    [InlineData("editor", "recent", null, 256, "invalid_source")]
    [InlineData("runtime", "wrong", null, 256, "invalid_view")]
    [InlineData("runtime", "recent", "abc", 256, "invalid_session_id")]
    [InlineData("runtime", "recent", null, 0, "invalid_limit")]
    [InlineData("runtime", "recent", null, 257, "invalid_limit")]
    public void InvalidReadsNeverEnterNative(string source, string view, string? session, int limit, string code)
    {
        var access = new Access(new Api());
        var error = Assert.Throws<ReiMcpOperationException>(() => new EngineProfilingService(access).Read(source, view, session, limit));
        Assert.Equal(code, error.Code);
        Assert.Equal(0, access.Calls);
    }

    [Fact]
    public void UnavailableUnsupportedAndMalformedRemainDistinct()
    {
        var api = new Api();
        var access = new Access(api) { Available = false };
        var service = new EngineProfilingService(access);
        Assert.Equal("engine_unavailable", service.Read("runtime", "recent", null, 256).GetProperty("status").GetString());
        access.Available = true;
        api.Supported = false;
        Assert.Equal("unsupported", service.Read("runtime", "recent", null, 256).GetProperty("status").GetString());
        Assert.Null(api.Method);
        api.Supported = true;
        api.Response = "bad json";
        Assert.Equal("read_failed", service.Read("runtime", "recent", null, 256).GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void InvalidCaptureDoesNotEnterNative(int frames)
    {
        var access = new Access(new Api());
        Assert.Throws<ReiMcpOperationException>(() => new EngineProfilingService(access).StartCapture(frames));
        Assert.Equal(0, access.Calls);
    }
}
