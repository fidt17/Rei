using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;

namespace ReiEditor.Views.Windows.Editor.Diagnostics;

public sealed class FrameTimeChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<FrameTimeSample>> SamplesProperty = AvaloniaProperty.Register<FrameTimeChart, IReadOnlyList<FrameTimeSample>>(nameof(Samples), Array.Empty<FrameTimeSample>());
    public static readonly StyledProperty<int> TargetFpsProperty = AvaloniaProperty.Register<FrameTimeChart, int>(nameof(TargetFps), 60);
    private static readonly IBrush GRID = new SolidColorBrush(Color.Parse("#30363d"));
    private static readonly Typeface FONT = new("Segoe UI");

    public IReadOnlyList<FrameTimeSample> Samples { get => GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public int TargetFps { get => GetValue(TargetFpsProperty); set => SetValue(TargetFpsProperty, value); }

    static FrameTimeChart() => AffectsRender<FrameTimeChart>(SamplesProperty, TargetFpsProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 120 || Bounds.Height < 60) return;
        var plot = new Rect(42, 6, Bounds.Width - 54, Bounds.Height - 30);
        var budget = 1000.0 / Math.Max(1, TargetFps);
        var maximum = Math.Max(40, Math.Ceiling(Math.Min(1e6, Math.Max(budget, Samples.Count > 0 ? Samples.Max(sample => sample.Milliseconds) : 0)) * 1.2 / 10) * 10);
        var end = Samples.Count > 0 ? Samples[^1].Seconds : 0;
        double y(double milliseconds) => plot.Bottom - Math.Clamp(milliseconds / maximum, 0, 1) * plot.Height;
        double x(double seconds) => plot.Right - Math.Clamp((end - seconds) / FrameTimeHistory.WINDOW_SECONDS, 0, 1) * plot.Width;
        for (var index = 0; index <= 2; index++)
        {
            var value = maximum * index / 2;
            context.DrawLine(new Pen(GRID, 0.5), new Point(plot.Left, y(value)), new Point(plot.Right, y(value)));
            DrawLabel(context, ProfilingPresentation.Number(value, "0"), 2, y(value) - 6);
        }
        for (var index = 0; index <= 4; index++)
        {
            var position = plot.Left + index * plot.Width / 4;
            context.DrawLine(new Pen(GRID, 0.5), new Point(position, plot.Top), new Point(position, plot.Bottom));
            var text = index == 4 ? "0 s" : $"−{20 - index * 5} s";
            DrawLabel(context, text, Math.Clamp(position - 14, 0, Bounds.Width - 30), plot.Bottom + 5);
        }
        context.DrawLine(new Pen(ProfilingPresentation.NEUTRAL, 1, DashStyle.Dash), new Point(plot.Left, y(budget)), new Point(plot.Right, y(budget)));
        using (context.PushClip(plot))
        {
            for (var index = 1; index < Samples.Count; index++)
            {
                var previous = Samples[index - 1];
                var current = Samples[index];
                if (current.Seconds - previous.Seconds > FrameTimeHistory.MAX_GAP_SECONDS) continue;
                context.DrawLine(new Pen(ProfilingPresentation.FrameColor(current.Milliseconds, TargetFps), 1.5), new Point(x(previous.Seconds), y(previous.Milliseconds)), new Point(x(current.Seconds), y(current.Milliseconds)));
            }
            if (Samples.Count > 0)
            {
                var last = Samples[^1];
                context.DrawEllipse(ProfilingPresentation.FrameColor(last.Milliseconds, TargetFps), null, new Point(x(last.Seconds), y(last.Milliseconds)), 2, 2);
            }
        }
        if (Samples.Count == 0) DrawLabel(context, "Waiting for samples", plot.Left + 8, plot.Top + 8);
    }

    private static void DrawLabel(DrawingContext context, string text, double x, double y)
    {
        var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, FONT, 11, ProfilingPresentation.NEUTRAL);
        context.DrawText(label, new Point(x, y));
    }
}
