using System.Text;
using System.Text.Json;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine;

public sealed class AssetRuntimeInspectionServiceTests
{
    private sealed class NativeApi : TestEngineApi
    {
        public bool Running = true;
        public string Json = "{\"status\":\"unloaded\",\"values\":null}";
        public int Calls;
        public int? RequiredOverride;
        public override bool IsEngineRunning => Running;
        public override T Invoke<T>(Type delegateType, string methodName, params object?[]? args)
        {
            Assert.Equal("GetLoadedAssetState", methodName);
            Assert.Equal("asset", args![0]);
            Calls++;
            var bytes = Encoding.UTF8.GetBytes(Json + "\0");
            var buffer = (byte[])args[1]!;
            if (bytes.Length <= buffer.Length) bytes.CopyTo(buffer, 0);
            return (T)(object)(RequiredOverride ?? bytes.Length);
        }
    }

    [Fact]
    public void StoppedEngineIsNotInvoked()
    {
        var api = new NativeApi { Running = false };
        Assert.Equal("engine_unavailable", new AssetRuntimeInspectionService(api).Read("asset").Status);
        Assert.Equal(0, api.Calls);
    }

    [Theory]
    [InlineData("unloaded")]
    [InlineData("unsupported")]
    [InlineData("read_failed")]
    public void PreservesNativeStatusWithoutEditorFallback(string status)
    {
        var api = new NativeApi { Json = JsonSerializer.Serialize(new { status, values = (object?)null }) };
        var result = new AssetRuntimeInspectionService(api).Read("asset");
        Assert.Equal(status, result.Status);
        Assert.Equal("runtime", result.Source);
        Assert.Null(result.Values);
    }

    [Fact]
    public void ResizesBufferAndPreservesUnicode()
    {
        var label = new string('x', 20000) + " Привет 世界";
        var api = new NativeApi { Json = JsonSerializer.Serialize(new { status = "loaded", values = new { label } }) };
        var result = new AssetRuntimeInspectionService(api).Read("asset");
        Assert.Equal(label, result.Values!.Value.GetProperty("label").GetString());
        Assert.Equal(2, api.Calls);
    }

    [Fact]
    public void RejectsUnboundedAllocation()
    {
        var api = new NativeApi { RequiredOverride = int.MaxValue };
        Assert.Equal("too_large", new AssetRuntimeInspectionService(api).Read("asset").Status);
        Assert.Equal(1, api.Calls);
    }
}
