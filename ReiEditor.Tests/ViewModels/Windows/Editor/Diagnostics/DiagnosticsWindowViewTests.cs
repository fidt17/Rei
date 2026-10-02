using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Text.Json;
using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Views.Windows.Editor.Diagnostics;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Diagnostics;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
public sealed class DiagnosticsWindowViewTests
{
    private sealed class Profiling : IEngineProfilingService
    {
        public bool ContinuousEnabled { get; private set; }
        public List<bool> Controls { get; } = new();
        public JsonElement Read(string source, string view, string? expectedSessionId, int limit) => JsonSerializer.SerializeToElement(new ProfilingSnapshot
        {
            Status = "ok", SessionId = "123", EngineMode = "PlayMode", CompletedFrames = 120, SampleFrames = 120,
            CompleteData = true, ContinuousEnabled = ContinuousEnabled, AverageFrameMs = 20, MaxFrameMs = 30, Fps = 50,
            Metrics = Enumerable.Range(0, 30).Select(index => new ProfilingMetric { Name = $"Rei.Render.{index:D2}", Kind = "scope", Calls = 120, AverageExclusiveMs = 6 + index }).ToArray()
        });
        public JsonElement StartCapture(int frameCount) => throw new NotSupportedException();
        public JsonElement SetContinuous(bool enabled, string? expectedSessionId)
        {
            Assert.Equal("123", expectedSessionId);
            Controls.Add(enabled);
            ContinuousEnabled = enabled;
            return Read("runtime", "recent", expectedSessionId, 256);
        }
    }

    [AvaloniaFact]
    public void WindowLoadsAtMinimumSizeWithoutFooterAndControlsFit()
    {
        var theme = new FluentTheme();
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Add(styles);
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        var window = new DiagnosticsWindowView { Width = 1040, Height = 560, DataContext = vm };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var start = window.FindControl<Button>("StartLiveButton")!;
            var stop = window.FindControl<Button>("StopLiveButton")!;
            Assert.Null(window.FindControl<Button>("CaptureButton"));
            Assert.Empty(window.GetVisualDescendants().OfType<CheckBox>());
            Assert.Equal(2, window.GetVisualDescendants().OfType<ComboBox>().Count());
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Content is "Re-sort" or "Capture");
            var export = window.FindControl<Button>("ExportButton")!;
            Assert.Equal(30, start.Bounds.Width);
            Assert.True(start.IsVisible);
            Assert.False(stop.IsVisible);
            Assert.True(export.Bounds.Width > 0);
            Assert.True(window.FindControl<ListBox>("ScopeList")!.Bounds.Height > 100);
            Assert.True(window.FindControl<ListBox>("CounterList") != null);
            Assert.True(window.CanResize);
            Assert.True(export.IsEnabled);
            var startRight = start.TranslatePoint(new Point(start.Bounds.Width, 0), window)!.Value.X;
            var exportLeft = export.TranslatePoint(new Point(0, 0), window)!.Value.X;
            Assert.True(startRight < exportLeft);
            var summary = window.FindControl<Border>("SummaryPanel")!;
            var toolbar = window.FindControl<Grid>("RecordingToolbar")!;
            Assert.True(summary.TranslatePoint(new Point(0, summary.Bounds.Height), window)!.Value.Y < toolbar.TranslatePoint(default, window)!.Value.Y);
            var scopes = window.FindControl<ListBox>("ScopeList")!;
            var first = (ListBoxItem)scopes.ContainerFromIndex(0)!;
            var second = (ListBoxItem)scopes.ContainerFromIndex(1)!;
            Assert.NotEqual(((ISolidColorBrush)first.Background!).Color, ((ISolidColorBrush)second.Background!).Color);
            Assert.NotEqual(((ISolidColorBrush)window.FindControl<Border>("ScopeHeader")!.Background!).Color, ((ISolidColorBrush)first.Background!).Color);
            // Numeric headings and values share the same right edge, including with scrolling rows.
            var headerTexts = window.FindControl<Border>("ScopeHeader")!.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var rowTexts = first.GetVisualDescendants().OfType<TextBlock>().ToArray();
            foreach (var pair in new[] { (vm.ExclusiveHeading, 1), (vm.InclusiveHeading, 2), (vm.MaximumHeading, 3), (vm.CallsHeading, 4) })
            {
                var heading = headerTexts.Single(text => text.Text == pair.Item1);
                var value = rowTexts[pair.Item2];
                var headingRight = heading.TranslatePoint(new Point(heading.Bounds.Width, 0), window)!.Value.X;
                var valueRight = value.TranslatePoint(new Point(value.Bounds.Width, 0), window)!.Value.X;
                Assert.InRange(Math.Abs(headingRight - valueRight), 0, 6);
            }
            // Global hover borders must not resize sorting headers or shift the table.
            var header = window.FindControl<Border>("ScopeHeader")!;
            var headerHeight = header.Bounds.Height;
            var rowsTop = scopes.TranslatePoint(default, window)!.Value.Y;
            foreach (var button in header.GetVisualDescendants().OfType<Button>())
            {
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsPointerOver);
                Assert.Equal(headerHeight, header.Bounds.Height);
                Assert.Equal(rowsTop, scopes.TranslatePoint(default, window)!.Value.Y);
                window.MouseDown(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(headerHeight, header.Bounds.Height);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }
            void Click(Button button)
            {
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }
            Click(start);
            Assert.True(vm.IsLive);
            Assert.False(start.IsVisible);
            Assert.True(stop.IsVisible);
            Assert.Equal(30, stop.Bounds.Width);
            Click(stop);
            Assert.False(vm.IsLive);
            Assert.True(start.IsVisible);
            Assert.False(stop.IsVisible);
            Assert.Equal(new[] { true, false }, profiling.Controls);
            var selected = scopes.SelectedItem = vm.Scopes[0];
            var listHeight = scopes.Bounds.Height;
            vm.ShowTrend = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<FrameTimeChart>("TrendChart")!.IsVisible);
            Assert.True(scopes.Bounds.Height > listHeight + 80);
            vm.Refresh();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(selected, scopes.SelectedItem);
            foreach (var toggle in window.GetVisualDescendants().OfType<ToggleButton>().Where(button => button.Content is "CPU scopes" or "Counters" or "Freeze"))
            {
                var presenter = toggle.GetVisualDescendants().OfType<ContentPresenter>().First(child => child.Name == "PART_ContentPresenter");
                Assert.Equal(Color.Parse("#e6edf3"), ((ISolidColorBrush)presenter.Foreground!).Color);
            }
            vm.ShowCountersCommand.Execute(null);
            vm.IsFrozen = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<ListBox>("CounterList")!.IsVisible);
            foreach (var toggle in window.GetVisualDescendants().OfType<ToggleButton>().Where(button => button.Content is "CPU scopes" or "Counters" or "Freeze"))
            {
                var presenter = toggle.GetVisualDescendants().OfType<ContentPresenter>().First(child => child.Name == "PART_ContentPresenter");
                Assert.Equal(Color.Parse("#e6edf3"), ((ISolidColorBrush)presenter.Foreground!).Color);
            }
            window.Width = 1200;
            window.Height = 760;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<ListBox>("CounterList")!.Bounds.Height > 300);
        }
        finally { window.Close(); Application.Current.Styles.Remove(styles); Application.Current.Styles.Remove(theme); }
    }
    [AvaloniaFact]
    public void MainEditorToolbarShowsDiagnosticsBeforeRelease()
    {
        var theme = new FluentTheme();
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Add(styles);
        var window = new ReiEditor.Views.Windows.Editor.ProjectEditorWindow { Width = 1040, Height = 760, WindowState = WindowState.Normal };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var diagnostics = window.FindControl<Button>("DiagnosticsButton")!;
            var release = window.GetVisualDescendants().OfType<Button>().Single(button => button.Content is string text && text.Trim() == "Release");
            Assert.Equal("Diagnostics", diagnostics.Content);
            Assert.True(diagnostics.IsVisible);
            Assert.True(diagnostics.Bounds.Width > 70);
            var right = diagnostics.TranslatePoint(new Point(diagnostics.Bounds.Width, 0), window)!.Value.X;
            var releaseLeft = release.TranslatePoint(default, window)!.Value.X;
            Assert.True(right <= releaseLeft);
            Assert.InRange(diagnostics.TranslatePoint(default, window)!.Value.Y, 0, 80);
        }
        finally { window.Close(); Application.Current.Styles.Remove(styles); Application.Current.Styles.Remove(theme); }
    }
}
