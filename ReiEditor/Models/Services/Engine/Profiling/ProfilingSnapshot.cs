using System;
using System.Text.Json;

namespace ReiEditor.Models.Services.Engine.Profiling;

public sealed record ProfilingMetric
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public double AverageInclusiveMs { get; init; }
    public double AverageExclusiveMs { get; init; }
    public double MaxFrameMs { get; init; }
    public ulong Calls { get; init; }
    public ulong Value { get; init; }
    public double AverageValue { get; init; }
    public ulong MaxFrameValue { get; init; }
}

public sealed record ProfilingSnapshot
{
    private static readonly JsonSerializerOptions OPTIONS = new() { PropertyNameCaseInsensitive = true };

    public string Status { get; init; } = "read_failed";
    public string SessionId { get; init; } = "";
    public string CaptureId { get; init; } = "0";
    public string EngineMode { get; init; } = "";
    public string CaptureState { get; init; } = "idle";
    public bool ContinuousEnabled { get; init; }
    public bool CompleteData { get; init; }
    public int TargetFrames { get; init; }
    public int CompletedFrames { get; init; }
    public int SampleFrames { get; init; }
    public int InvalidFrames { get; init; }
    public ulong LastFrame { get; init; }
    public double AverageFrameMs { get; init; }
    public double MaxFrameMs { get; init; }
    public double Fps { get; init; }
    public ProfilingMetric[] Metrics { get; init; } = Array.Empty<ProfilingMetric>();

    public static ProfilingSnapshot Read(JsonElement json) => json.Deserialize<ProfilingSnapshot>(OPTIONS) ?? new();
}
