using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Engine.Profiling;

namespace ReiEditor.ViewModels.Windows.Editor.Diagnostics;

public readonly record struct FrameTimeSample(double Seconds, double Milliseconds);

// Editor-side history of published averages, not a per-frame trace.
public sealed class FrameTimeHistory
{
    public const double WINDOW_SECONDS = 20;
    public const double MAX_GAP_SECONDS = 1.5;
    private const int MAX_SAMPLES = 128;
    private readonly List<FrameTimeSample> _samples = new();
    private ulong _lastFrame;
    private int _completedFrames;
    private bool _hasSample;

    public IReadOnlyList<FrameTimeSample> Samples => _samples;

    public bool Append(ProfilingSnapshot snapshot, double seconds)
    {
        if (snapshot.SampleFrames <= 0 || !ProfilingPresentation.IsValidTime(snapshot.AverageFrameMs) || !double.IsFinite(seconds)) return false;
        if (_hasSample && snapshot.LastFrame == _lastFrame && snapshot.CompletedFrames == _completedFrames) return false;
        if (_samples.Count > 0 && seconds < _samples[^1].Seconds) Clear();
        _lastFrame = snapshot.LastFrame;
        _completedFrames = snapshot.CompletedFrames;
        _hasSample = true;
        _samples.RemoveAll(sample => sample.Seconds < seconds - WINDOW_SECONDS);
        _samples.Add(new FrameTimeSample(seconds, snapshot.AverageFrameMs));
        if (_samples.Count > MAX_SAMPLES) _samples.RemoveRange(0, _samples.Count - MAX_SAMPLES);
        return true;
    }

    public void Clear()
    {
        _samples.Clear();
        _hasSample = false;
    }
}
