using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ReiEditor.Mcp.Contracts;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;

namespace ReiEditor.Models.Services.Engine.Profiling;

public sealed class EngineProfilingService(IEngineNativeAccess nativeAccess) : IEngineProfilingService
{
    private delegate int ReadDelegate(string view, string expectedSessionId, int limit, [Out] byte[] buffer, int bufferSize);
    private delegate int CaptureDelegate(int frameCount, [Out] byte[] buffer, int bufferSize);
    private const int BUFFER_SIZE = 256 * 1024;
    private const int MAX_METRICS = 256;
    private const int MAX_CAPTURE_FRAMES = 3600;

    public JsonElement Read(string source, string view, string? expectedSessionId, int limit)
    {
        if (source != "runtime") throw new ReiMcpOperationException("invalid_source", "Profiling source must be runtime.");
        if (view is not ("recent" or "last_capture")) throw new ReiMcpOperationException("invalid_view", "View must be recent or last_capture.");
        if (limit < 1 || limit > MAX_METRICS) throw new ReiMcpOperationException("invalid_limit", "Limit must be between 1 and 256.");
        if (expectedSessionId != null && !ulong.TryParse(expectedSessionId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            throw new ReiMcpOperationException("invalid_session_id", "Session id must be an unsigned decimal string.");
        return Invoke("GetProfilingSnapshot", api =>
        {
            var buffer = new byte[BUFFER_SIZE];
            var size = api.Invoke<int>(typeof(ReadDelegate), "GetProfilingSnapshot", view, expectedSessionId ?? "", limit, buffer, buffer.Length);
            return Decode(buffer, size);
        });
    }

    public JsonElement StartCapture(int frameCount)
    {
        if (frameCount < 1 || frameCount > MAX_CAPTURE_FRAMES)
            throw new ReiMcpOperationException("invalid_frame_count", "Frame count must be between 1 and 3600.");
        return Invoke("StartProfilingCapture", api =>
        {
            var buffer = new byte[4096];
            var size = api.Invoke<int>(typeof(CaptureDelegate), "StartProfilingCapture", frameCount, buffer, buffer.Length);
            return Decode(buffer, size);
        });
    }

    private JsonElement Invoke(string export, Func<IEngineApi, JsonElement> call)
    {
        var result = Status("engine_unavailable");
        nativeAccess.TryInvoke(api =>
        {
            if (!api.HasExport(export))
            {
                result = Status("unsupported");
                return;
            }
            try { result = call(api); }
            catch (Exception) { result = Status("read_failed"); }
        });
        return result;
    }

    private static JsonElement Decode(byte[] buffer, int size)
    {
        if (size < 2 || size > buffer.Length) return Status("read_failed");
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, size - 1));
        return document.RootElement.Clone();
    }

    private static JsonElement Status(string status) => JsonSerializer.SerializeToElement(new { source = "runtime", status });
}
