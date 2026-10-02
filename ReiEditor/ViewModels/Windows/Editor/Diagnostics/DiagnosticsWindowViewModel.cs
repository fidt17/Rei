using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using ReactiveUI;
using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.Utils;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Diagnostics;

public sealed class DiagnosticsWindowViewModel : BaseViewModel
{
    public RelayCommand StartLiveCommand { get; }
    public RelayCommand StopLiveCommand { get; }
    public RelayCommand SortNameCommand { get; }
    public RelayCommand SortCallsCommand { get; }
    public RelayCommand SortInclusiveCommand { get; }
    public RelayCommand SortExclusiveCommand { get; }
    public RelayCommand SortMaximumCommand { get; }
    public RelayCommand ShowScopesCommand { get; }
    public RelayCommand ShowCountersCommand { get; }
    public int[] TargetFpsOptions { get; } = { 30, 60, 120, 144 };
    public string[] FilterOptions { get; } = { "All scopes", "Engine", "Project" };

    private int _targetFps = 60;
    public int TargetFps { get => _targetFps; set { if (value is >= 1 and <= 1000 && SetField(ref _targetFps, value)) UpdateDisplay(); } }
    private string _filter = "All scopes";
    public string Filter { get => _filter; set { if (SetField(ref _filter, value)) UpdateRows(); } }
    private string _search = "";
    public string Search { get => _search; set { if (SetField(ref _search, value)) UpdateRows(); } }
    private bool _isLive;
    public bool IsLive { get => _isLive; set => SetLive(value); }
    private bool _isFrozen;
    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            if (!SetField(ref _isFrozen, value)) return;
            if (!value) { Refresh(); return; }
            _resortRows = true;
            UpdateRows();
        }
    }
    private bool _showTrend = true;
    public bool ShowTrend { get => _showTrend; set => SetField(ref _showTrend, value); }
    private IReadOnlyList<FrameTimeSample> _trendSamples = Array.Empty<FrameTimeSample>();
    public IReadOnlyList<FrameTimeSample> TrendSamples { get => _trendSamples; private set => SetField(ref _trendSamples, value); }
    public string TrendCaption => "Frame time · averaged trend · 20 s";
    public string FrameBudget => $"{ProfilingPresentation.Number(1000.0 / TargetFps)} ms · {TargetFps} FPS";
    private bool _showScopes = true;
    public bool ShowScopes { get => _showScopes; private set { if (SetField(ref _showScopes, value)) this.RaisePropertyChanged(nameof(ShowCounters)); } }
    public bool ShowCounters => !ShowScopes;
    public ObservableCollection<ProfilingRow> Scopes { get; } = new();
    public ObservableCollection<ProfilingRow> Counters { get; } = new();
    private string _connection = "Waiting for engine";
    public string Connection { get => _connection; private set => SetField(ref _connection, value); }
    private string _message = "Recording is off. Press Start to enable Live diagnostics.";
    public string Message { get => _message; private set => SetField(ref _message, value); }
    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool HasData => _display?.CompletedFrames > 0;
    public string FrameAverage => HasData ? ProfilingPresentation.Time(_display!.AverageFrameMs) + " ms" : "—";
    public string FrameMaximum => HasData ? ProfilingPresentation.Time(_display!.MaxFrameMs) + " ms" : "—";
    public string Fps => HasData ? ProfilingPresentation.Number(_display!.Fps, "0.0") : "—";
    public string Draws => CounterValue("Rei.Draw.Calls");
    public string Triangles => CounterValue("Rei.Draw.Triangles");
    public IBrush FrameColor => HasData ? ProfilingPresentation.FrameColor(_display!.AverageFrameMs, TargetFps) : ProfilingPresentation.NEUTRAL;
    public IBrush MaximumColor => HasData ? ProfilingPresentation.FrameColor(_display!.MaxFrameMs, TargetFps) : ProfilingPresentation.NEUTRAL;
    public string NameHeading => Heading("Scope", "Name");
    public string ExclusiveHeading => Heading("Exclusive ms", "Exclusive");
    public string InclusiveHeading => Heading("Inclusive ms", "Inclusive");
    public string MaximumHeading => Heading("Max incl., ms", "Maximum");
    public string CallsHeading => Heading("Calls / frame", "Calls");
    public string SampleLabel => HasData ? $"{_display!.EngineMode} · {_display.SampleFrames} valid / {_display.CompletedFrames} frames" : "No samples";

    private readonly IEngineProfilingService _profiling;
    private readonly TimeProvider _clock;
    private readonly FrameTimeHistory _history = new();
    private readonly Dictionary<(string Kind, string Name), ProfilingRow> _rows = new();
    private readonly List<string> _scopeOrder = new();
    private readonly List<string> _counterOrder = new();
    private string? _displayKey;
    private bool _resortRows = true;
    private bool _hasSortedSamples;
    private ProfilingSnapshot? _display;
    private JsonElement? _displayJson;
    private string? _sessionId;
    private string? _ownedLiveSession;
    private string _sort = "Exclusive";
    private bool _ascending;
    private bool _disposed;

    public DiagnosticsWindowViewModel(IEngineProfilingService profiling, TimeProvider? clock = null)
    {
        _profiling = profiling;
        _clock = clock ?? TimeProvider.System;
        StartLiveCommand = new RelayCommand(() => IsLive = true, () => !_disposed);
        StopLiveCommand = new RelayCommand(() => IsLive = false, () => !_disposed);
        SortNameCommand = new RelayCommand(() => Sort("Name"));
        SortCallsCommand = new RelayCommand(() => Sort("Calls"));
        SortInclusiveCommand = new RelayCommand(() => Sort("Inclusive"));
        SortExclusiveCommand = new RelayCommand(() => Sort("Exclusive"));
        SortMaximumCommand = new RelayCommand(() => Sort("Maximum"));
        ShowScopesCommand = new RelayCommand(() => ShowScopes = true);
        ShowCountersCommand = new RelayCommand(() => ShowScopes = false);
    }

    public void Refresh()
    {
        if (_disposed) return;
        try
        {
            var json = _profiling.Read("runtime", "recent", _sessionId, 256);
            var snapshot = ProfilingSnapshot.Read(json);
            if (snapshot.Status is "engine_unavailable" or "unsupported" or "read_failed")
            {
                ResetDisplay();
                Connection = snapshot.Status switch { "engine_unavailable" => "Engine unavailable", "unsupported" => "Profiling unavailable in this DLL", _ => "Native read failed" };
                return;
            }
            var changed = _sessionId != null && _sessionId != snapshot.SessionId;
            _sessionId = snapshot.SessionId;
            if (changed)
            {
                _ownedLiveSession = null;
                _isFrozen = false;
                this.RaisePropertyChanged(nameof(IsFrozen));
            }
            SetField(ref _isLive, snapshot.ContinuousEnabled, nameof(IsLive));
            Connection = $"{snapshot.EngineMode} · Connected";
            if (IsFrozen && !changed) return;
            var displayKey = snapshot.SessionId;
            if (_displayKey != displayKey)
            {
                ClearPresentation();
                _displayKey = displayKey;
            }
            _display = snapshot;
            if (!_hasSortedSamples && snapshot.SampleFrames > 0) { _resortRows = true; _hasSortedSamples = true; }
            _displayJson = json.Clone();
            if (_history.Append(snapshot, (double)_clock.GetTimestamp() / _clock.TimestampFrequency)) TrendSamples = _history.Samples.ToArray();
            Message = snapshot.CompletedFrames == 0 ? (snapshot.Status == "disabled" ? "Recording is off. Press Start to enable Live diagnostics." : "Waiting for completed frames…")
                : !snapshot.CompleteData ? $"Incomplete samples: {snapshot.InvalidFrames} invalid frames excluded from metric averages."
                : snapshot.Status == "disabled" ? "Recording is off. Showing retained samples." : "";
            UpdateDisplay();
        }
        catch (Exception)
        {
            ResetDisplay();
            Connection = "Native read failed";
        }
    }

    public string? ExportJson() => _displayJson?.GetRawText();

    public void ReportExportError() { Message = "Could not export snapshot."; this.RaisePropertyChanged(nameof(HasMessage)); }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownedLiveSession != null) _profiling.SetContinuous(false, _ownedLiveSession);
        _ownedLiveSession = null;
        StartLiveCommand.InvokeCanExecuteChanged();
        StopLiveCommand.InvokeCanExecuteChanged();
        base.Dispose();
    }

    private void SetLive(bool enabled)
    {
        if (_disposed || enabled == _isLive) return;
        var result = ProfilingSnapshot.Read(_profiling.SetContinuous(enabled, _sessionId));
        if (result.Status != "ok") { Connection = $"Live control: {result.Status}"; this.RaisePropertyChanged(nameof(IsLive)); return; }
        if (enabled && !IsLive) _ownedLiveSession = result.SessionId;
        if (!enabled) _ownedLiveSession = null;
        _sessionId = result.SessionId;
        SetField(ref _isLive, result.ContinuousEnabled, nameof(IsLive));
        Refresh();
    }

    private void ResetDisplay()
    {
        _sessionId = null;
        _isFrozen = false;
        this.RaisePropertyChanged(nameof(IsFrozen));
        // Keep ownership until close or a confirmed new session; expectedSessionId prevents touching a replacement engine.
        SetField(ref _isLive, false, nameof(IsLive));
        _display = null;
        _displayJson = null;
        ClearPresentation();
        Message = "No runtime data. Last session values have been cleared.";
        UpdateDisplay();
    }

    private void Sort(string column)
    {
        _ascending = _sort == column ? !_ascending : column == "Name";
        _sort = column;
        _resortRows = true;
        UpdateRows();
        foreach (var name in new[] { nameof(NameHeading), nameof(ExclusiveHeading), nameof(InclusiveHeading), nameof(MaximumHeading), nameof(CallsHeading) }) this.RaisePropertyChanged(name);
    }

    private string Heading(string title, string column) => _sort == column ? $"{title} {(_ascending ? "↑" : "↓")}" : title;

    private void UpdateDisplay()
    {
        foreach (var name in new[] { nameof(HasData), nameof(FrameAverage), nameof(FrameMaximum), nameof(Fps), nameof(Draws), nameof(Triangles), nameof(FrameColor), nameof(MaximumColor), nameof(HasMessage), nameof(SampleLabel), nameof(TrendCaption), nameof(FrameBudget) })
            this.RaisePropertyChanged(name);
        UpdateRows();
    }

    private void UpdateRows()
    {
        var metrics = _display?.Metrics ?? Array.Empty<ProfilingMetric>();
        var rows = metrics.Select(metric =>
        {
            var id = (metric.Kind, metric.Name);
            if (!_rows.TryGetValue(id, out var row)) _rows[id] = row = new ProfilingRow(metric, _display?.SampleFrames ?? 0, TargetFps);
            else row.Update(metric, _display?.SampleFrames ?? 0, TargetFps);
            return (Metric: metric, Row: row);
        }).ToArray();
        Func<ProfilingRow, double> key = _sort switch { "Calls" => row => row.Calls, "Inclusive" => row => row.Inclusive, "Maximum" => row => row.Maximum, _ => row => row.Exclusive };
        var scopes = rows.Where(item => item.Metric.Kind == "scope").Select(item => item.Row);
        var sortedScopes = (_sort == "Name" ? (_ascending ? scopes.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase) : scopes.OrderByDescending(row => row.Name, StringComparer.OrdinalIgnoreCase))
            : _ascending ? scopes.OrderBy(key).ThenBy(row => row.Name) : scopes.OrderByDescending(key).ThenBy(row => row.Name)).ToArray();
        var sortedCounters = rows.Where(item => item.Metric.Kind == "counter").Select(item => item.Row).OrderByDescending(row => row.AverageValue).ThenBy(row => row.Name).ToArray();
        UpdateCollection(Scopes, StableOrder(sortedScopes, _scopeOrder).Where(MatchesFilter).ToArray());
        UpdateCollection(Counters, StableOrder(sortedCounters, _counterOrder).Where(MatchesFilter).ToArray());
        _resortRows = false;
    }

    private bool MatchesFilter(ProfilingRow row) => row.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)
        && (Filter == "All scopes" || (Filter == "Engine" ? row.IsEngine : !row.IsEngine));

    private IEnumerable<ProfilingRow> StableOrder(ProfilingRow[] sorted, List<string> order)
    {
        var present = sorted.ToDictionary(row => row.Name);
        if (_resortRows) order.Clear();
        order.RemoveAll(name => !present.ContainsKey(name));
        var existing = order.ToHashSet();
        order.AddRange(sorted.Where(row => !existing.Contains(row.Name)).Select(row => row.Name));
        return order.Select(name => present[name]);
    }

    private static void UpdateCollection(ObservableCollection<ProfilingRow> collection, ProfilingRow[] desired)
    {
        var visible = desired.ToHashSet();
        for (var index = collection.Count - 1; index >= 0; index--)
            if (!visible.Contains(collection[index])) collection.RemoveAt(index);
        for (var index = 0; index < desired.Length; index++)
        {
            if (index < collection.Count && collection[index] == desired[index]) continue;
            var previous = collection.IndexOf(desired[index]);
            if (previous < 0) collection.Insert(index, desired[index]);
            else collection.Move(previous, index);
        }
    }

    private void ClearPresentation()
    {
        _displayKey = null;
        _rows.Clear();
        _scopeOrder.Clear();
        _counterOrder.Clear();
        Scopes.Clear();
        Counters.Clear();
        _history.Clear();
        TrendSamples = Array.Empty<FrameTimeSample>();
        _resortRows = true;
        _hasSortedSamples = false;
    }

    private string CounterValue(string name) => HasData && _display!.SampleFrames > 0
        ? ProfilingPresentation.Number(_display.Metrics.FirstOrDefault(metric => metric.Name == name)?.AverageValue ?? 0, "#,0.##") : "—";
}
