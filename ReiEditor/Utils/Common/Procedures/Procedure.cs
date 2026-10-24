using System;

namespace ReiEditor.Utils.Common.Procedures;

public class Procedure : IProcedure
{
    public event Action? FinishedEvent;
    public event Action? ProgressChangedEvent;

    public string Name { get; }
    public bool Finished { get { lock (_sync) return _finished; } }
    public double? Progress { get { lock (_sync) return _progress; } }

    private readonly object _sync = new();
    private bool _finished;
    private double? _progress;

    public Procedure(string name)
    {
        Name = name;
    }

    public void ReportProgress(double progress)
    {
        if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        progress = Math.Clamp(progress, 0, 1);
        lock (_sync)
        {
            if (_finished || _progress >= progress) return;
            _progress = progress;
        }
        ProgressChangedEvent?.Invoke();
    }

    public void Complete()
    {
        lock (_sync)
        {
            if (_finished) return;
            _finished = true;
        }
        FinishedEvent?.Invoke();
    }
}
