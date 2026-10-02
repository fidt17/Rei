using System.Text.Json;

namespace ReiEditor.Models.Services.Engine.Profiling;

public interface IEngineProfilingService
{
    JsonElement Read(string source, string view, string? expectedSessionId, int limit);
    JsonElement StartCapture(int frameCount);
    JsonElement SetContinuous(bool enabled, string? expectedSessionId);
}
