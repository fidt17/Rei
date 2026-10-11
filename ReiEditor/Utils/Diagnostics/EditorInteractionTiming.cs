using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace ReiEditor.Utils.Diagnostics;

// Opt-in wall-clock traces. File I/O runs off the UI thread.
public static class EditorInteractionTiming
{
    private sealed record Sample(string Stage, long StartTicks, long EndTicks, double ElapsedMs, int ThreadId, int EntityId, int Count);
    public readonly struct Scope(string stage, int entityId, int count) : IDisposable
    {
        private readonly long _start = Enabled ? Stopwatch.GetTimestamp() : 0;
        public void Dispose()
        {
            if (_start != 0) Record(stage, _start, Stopwatch.GetTimestamp(), entityId, count);
        }
    }

    private static readonly string? _path = Environment.GetEnvironmentVariable("REI_EDITOR_INTERACTION_TRACE");
    private static readonly ConcurrentQueue<Sample> _samples = new();
    private static readonly object _flushGate = new();
    private static readonly Timer? _timer = Enabled ? new Timer(_ => Flush(), null, 1000, 1000) : null;
    public static bool Enabled => !string.IsNullOrWhiteSpace(_path);
    public static Scope Measure(string stage, int entityId = 0, int count = 0) => new(stage, entityId, count);
    public static void Mark(string stage, int entityId = 0, int count = 0)
    {
        if (!Enabled) return;
        var now = Stopwatch.GetTimestamp();
        Record(stage, now, now, entityId, count);
    }

    private static void Record(string stage, long start, long end, int entityId, int count) =>
        _samples.Enqueue(new(stage, start, end, (end - start) * 1000.0 / Stopwatch.Frequency, Environment.CurrentManagedThreadId, entityId, count));

    public static void Flush()
    {
        if (!Enabled) return;
        lock (_flushGate)
        {
            try
            {
                var lines = new List<string>();
                while (_samples.TryDequeue(out var sample)) lines.Add(JsonSerializer.Serialize(sample));
                if (lines.Count > 0) File.AppendAllLines(_path!, lines);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
