using System;
using System.Globalization;
using ReactiveUI;
using Avalonia.Media;
using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Diagnostics;

public static class ProfilingPresentation
{
    public static readonly IBrush NEUTRAL = new SolidColorBrush(Color.Parse("#8E969D"));
    public static readonly IBrush GOOD = new SolidColorBrush(Color.Parse("#7FBF95"));
    public static readonly IBrush WARNING = new SolidColorBrush(Color.Parse("#FCC943"));
    public static readonly IBrush SLOW = new SolidColorBrush(Color.Parse("#F76F58"));

    public static string Number(double value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);
    public static string Time(double value) => value > 0 && value < 0.01 ? "<0.01" : Number(value);
    public static bool IsValidTime(double value) => double.IsFinite(value) && value > 0;
    public static IBrush FrameColor(double milliseconds, int targetFps)
    {
        if (!IsValidTime(milliseconds) || targetFps <= 0) return NEUTRAL;
        var ratio = milliseconds * targetFps / 1000;
        return ratio <= 1.05 ? GOOD : ratio <= 1.5 ? WARNING : SLOW;
    }

    public static IBrush ScopeColor(double milliseconds, int targetFps)
    {
        if (!IsValidTime(milliseconds) || targetFps <= 0) return NEUTRAL;
        var share = milliseconds * targetFps / 1000;
        return share < 0.05 ? GOOD : share < 0.2 ? WARNING : SLOW;
    }

    public static double ScopeBarWidth(double milliseconds, int targetFps) =>
        IsValidTime(milliseconds) && targetFps > 0 ? 120 * Math.Clamp(milliseconds * targetFps / 1000, 0, 1) : 0;
}

public sealed class ProfilingRow : BaseViewModel
{
    private ProfilingMetric metric = new();
    private int sampleFrames;
    private int targetFps;
    public string Name => metric.Name;
    public bool IsEngine => Name.StartsWith("Rei.", StringComparison.Ordinal);
    public double Calls => sampleFrames > 0 ? (double)metric.Calls / sampleFrames : 0;
    public double Inclusive => metric.AverageInclusiveMs;
    public double Exclusive => metric.AverageExclusiveMs;
    public double Maximum => metric.MaxFrameMs;
    public double AverageValue => metric.AverageValue;
    public ulong MaximumValue => metric.MaxFrameValue;
    public string CallsText => ProfilingPresentation.Number(Calls, "0.##");
    public string InclusiveText => ProfilingPresentation.Time(Inclusive);
    public string ExclusiveText => ProfilingPresentation.Time(Exclusive);
    public string MaximumText => ProfilingPresentation.Time(Maximum);
    public string InclusiveHint => $"{ProfilingPresentation.Number(Inclusive, "0.######")} ms per valid frame, including children. Inclusive totals overlap.";
    public string MaximumHint => $"{ProfilingPresentation.Number(Maximum, "0.######")} ms: largest inclusive cost within one valid frame.";
    public string AverageValueText => ProfilingPresentation.Number(AverageValue, "#,0.##");
    public string MaximumValueText => metric.MaxFrameValue.ToString("N0", CultureInfo.InvariantCulture);
    public string TotalText => metric.Value.ToString("N0", CultureInfo.InvariantCulture);
    public IBrush CostColor => ProfilingPresentation.ScopeColor(Exclusive, targetFps);
    public double CostBarWidth => ProfilingPresentation.ScopeBarWidth(Exclusive, targetFps);
    public string CostHint => $"{ProfilingPresentation.Number(Exclusive, "0.######")} ms per valid frame, excluding children. {ProfilingPresentation.Number(Exclusive * targetFps / 10, "0.0")}% of target frame budget.";

    public ProfilingRow(ProfilingMetric metric, int sampleFrames, int targetFps) => Update(metric, sampleFrames, targetFps);

    public void Update(ProfilingMetric metric, int sampleFrames, int targetFps)
    {
        if (this.metric == metric && this.sampleFrames == sampleFrames && this.targetFps == targetFps) return;
        this.metric = metric;
        this.sampleFrames = sampleFrames;
        this.targetFps = targetFps;
        foreach (var name in new[] { nameof(Name), nameof(IsEngine), nameof(Calls), nameof(Inclusive), nameof(Exclusive), nameof(Maximum), nameof(AverageValue), nameof(MaximumValue), nameof(CallsText), nameof(InclusiveText), nameof(ExclusiveText), nameof(MaximumText), nameof(AverageValueText), nameof(MaximumValueText), nameof(TotalText), nameof(CostColor), nameof(CostBarWidth), nameof(CostHint), nameof(InclusiveHint), nameof(MaximumHint) })
            this.RaisePropertyChanged(name);
    }
}
