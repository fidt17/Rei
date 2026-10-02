using System.Text.Json;
using Autofac;
using ReiEditor.Startup.Scopes.Editor.Modules;
using ReiEditor.Models.Services.Engine.Profiling;
using ReiEditor.ViewModels.Windows.Editor.Diagnostics;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Diagnostics;

public sealed class DiagnosticsWindowViewModelTests
{
    private sealed class Clock : TimeProvider
    {
        public double Seconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => (long)(Seconds * TimestampFrequency);
    }
    private sealed class Profiling : IEngineProfilingService
    {
        public ProfilingSnapshot Recent { get; set; } = Snapshot();
        public List<(bool Enabled, string? Session)> Controls { get; } = new();
        public List<string> Views { get; } = new();
        public JsonElement Read(string source, string view, string? expectedSessionId, int limit)
        {
            Assert.Equal("runtime", source);
            Views.Add(view);
            Assert.Equal("recent", view);
            return JsonSerializer.SerializeToElement(Recent);
        }
        public JsonElement StartCapture(int frameCount) => throw new NotSupportedException();
        public JsonElement SetContinuous(bool enabled, string? expectedSessionId)
        {
            Controls.Add((enabled, expectedSessionId));
            if (expectedSessionId != null && expectedSessionId != Recent.SessionId)
                return JsonSerializer.SerializeToElement(Recent with { Status = "session_changed" });
            Recent = Recent with { ContinuousEnabled = enabled };
            return JsonSerializer.SerializeToElement(Recent with { Status = "ok" });
        }
    }

    private static ProfilingSnapshot Snapshot() => new()
    {
        Status = "ok", SessionId = "123", EngineMode = "PlayMode", CompletedFrames = 100, SampleFrames = 100,
        CompleteData = true, AverageFrameMs = 16.67, MaxFrameMs = 28, Fps = 60,
        Metrics = new[]
        {
            new ProfilingMetric { Name = "Rei.Render", Kind = "scope", Calls = 100, AverageInclusiveMs = 8, AverageExclusiveMs = 4, MaxFrameMs = 10 },
            new ProfilingMetric { Name = "Symbols.Eye.Update", Kind = "scope", Calls = 200, AverageInclusiveMs = 3, AverageExclusiveMs = 0.2, MaxFrameMs = 5 },
            new ProfilingMetric { Name = "Rei.Draw.Calls", Kind = "counter", Value = 38800, AverageValue = 388, MaxFrameValue = 400 }
        }
    };

    [Fact]
    public void ReadDoesNotRecordAndRowsUseNativeValidSampleDenominator()
    {
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        Assert.Empty(profiling.Controls);
        Assert.Equal("recent", Assert.Single(profiling.Views));
        Assert.Equal("16.67 ms", vm.FrameAverage);
        Assert.Equal("388", vm.Draws);
        Assert.Equal("Rei.Render", vm.Scopes[0].Name);
        Assert.Equal(2, vm.Scopes[1].Calls);
        Assert.Single(vm.Counters);
        Assert.Equal("38,800", vm.Counters[0].TotalText);
        vm.Filter = "Project";
        Assert.Single(vm.Scopes);
        Assert.Empty(vm.Counters);
        vm.Filter = "All scopes";
        vm.Search = "EYE";
        Assert.Equal("Symbols.Eye.Update", Assert.Single(vm.Scopes).Name);
    }

    [Fact]
    public void FreezeKeepsDisplayedAndExportedSnapshotButSessionReplacementClearsIt()
    {
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        vm.IsFrozen = true;
        var exported = vm.ExportJson();
        profiling.Recent = profiling.Recent with { AverageFrameMs = 50, Fps = 20 };
        vm.Refresh();
        Assert.Equal("16.67 ms", vm.FrameAverage);
        Assert.Equal(exported, vm.ExportJson());
        profiling.Recent = profiling.Recent with { SessionId = "456", CompletedFrames = 0, SampleFrames = 0, Status = "disabled", Metrics = Array.Empty<ProfilingMetric>() };
        vm.Refresh();
        Assert.False(vm.IsFrozen);
        Assert.False(vm.HasData);
        Assert.Equal("—", vm.FrameAverage);
        Assert.Empty(vm.Scopes);
    }

    [Theory]
    [InlineData("engine_unavailable")]
    [InlineData("unsupported")]
    [InlineData("read_failed")]
    public void UnavailableDataNeverMasqueradesAsPreviousSession(string status)
    {
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        profiling.Recent = new ProfilingSnapshot { Status = status };
        vm.Refresh();
        Assert.False(vm.HasData);
        Assert.Null(vm.ExportJson());
        Assert.Empty(vm.Scopes);
        Assert.False(vm.IsLive);
        Assert.Empty(vm.TrendSamples);
    }

    [Fact]
    public void StartStopCommandsToggleContinuousRecordingAndRespectDisposal()
    {
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        Assert.False(vm.IsLive);
        vm.StartLiveCommand.Execute(null);
        Assert.True(vm.IsLive);
        vm.StartLiveCommand.Execute(null);
        Assert.Single(profiling.Controls);
        vm.StopLiveCommand.Execute(null);
        Assert.False(vm.IsLive);
        vm.StopLiveCommand.Execute(null);
        Assert.Equal(new[] { (true, (string?)"123"), (false, (string?)"123") }, profiling.Controls);
        vm.Dispose();
        Assert.False(vm.StartLiveCommand.CanExecute(null));
        Assert.False(vm.StopLiveCommand.CanExecute(null));
        vm.StartLiveCommand.Execute(null);
        Assert.Equal(2, profiling.Controls.Count);
    }

    [Fact]
    public void ClosingDisablesOnlyLiveStartedByWindowAndGuardsAgainstReplacementSession()
    {
        var profiling = new Profiling();
        var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        vm.IsLive = true;
        Assert.True(vm.IsLive);
        vm.Dispose();
        vm.Dispose();
        Assert.Equal(new[] { (true, (string?)"123"), (false, (string?)"123") }, profiling.Controls);
        Assert.False(profiling.Recent.ContinuousEnabled);

        var external = new Profiling { Recent = Snapshot() with { ContinuousEnabled = true } };
        var externalVm = new DiagnosticsWindowViewModel(external);
        externalVm.Refresh();
        externalVm.Dispose();
        Assert.Empty(external.Controls);

        var replaced = new Profiling();
        var oldVm = new DiagnosticsWindowViewModel(replaced);
        oldVm.Refresh();
        oldVm.IsLive = true;
        replaced.Recent = Snapshot() with { SessionId = "789", ContinuousEnabled = true };
        oldVm.Dispose();
        Assert.Equal((false, (string?)"123"), replaced.Controls[^1]);
        Assert.True(replaced.Recent.ContinuousEnabled);
    }

    [Fact]
    public void ColorAndBarsUseSelectedBudgetAndUnknownSamplesStayNeutral()
    {
        Assert.Same(ProfilingPresentation.GOOD, ProfilingPresentation.FrameColor(16.67, 60));
        Assert.Same(ProfilingPresentation.WARNING, ProfilingPresentation.FrameColor(22, 60));
        Assert.Same(ProfilingPresentation.SLOW, ProfilingPresentation.FrameColor(40, 60));
        Assert.Same(ProfilingPresentation.NEUTRAL, ProfilingPresentation.FrameColor(double.NaN, 60));
        Assert.Same(ProfilingPresentation.GOOD, ProfilingPresentation.ScopeColor(0.2, 60));
        Assert.Same(ProfilingPresentation.WARNING, ProfilingPresentation.ScopeColor(2, 60));
        Assert.Same(ProfilingPresentation.SLOW, ProfilingPresentation.ScopeColor(4, 60));
        Assert.Equal(120, ProfilingPresentation.ScopeBarWidth(40, 60));
        Assert.Equal(0, ProfilingPresentation.ScopeBarWidth(-1, 60));
        Assert.Same(ProfilingPresentation.SLOW, ProfilingPresentation.FrameColor(16.67, 120));
    }

    [Fact]
    public void LiveUpdatesRowsInPlaceAndOnlyExplicitSortOrFreezeChangesTheirOrder()
    {
        var profiling = new Profiling();
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        var first = vm.Scopes[0];
        var second = vm.Scopes[1];
        profiling.Recent = profiling.Recent with { Metrics = profiling.Recent.Metrics.Select(metric => metric.Name == "Symbols.Eye.Update" ? metric with { AverageExclusiveMs = 7.1234 } : metric).ToArray() };
        vm.Refresh();
        Assert.Same(first, vm.Scopes[0]);
        Assert.Same(second, vm.Scopes[1]);
        Assert.Equal("7.12", second.ExclusiveText);
        vm.Search = "Eye";
        Assert.Same(second, Assert.Single(vm.Scopes));
        vm.Search = "";
        Assert.Same(first, vm.Scopes[0]);
        vm.SortExclusiveCommand.Execute(null);
        Assert.Equal("Exclusive ms ↑", vm.ExclusiveHeading);
        vm.SortExclusiveCommand.Execute(null);
        Assert.Same(second, vm.Scopes[0]);
        profiling.Recent = profiling.Recent with { Metrics = profiling.Recent.Metrics.Select(metric => metric.Name == "Rei.Render" ? metric with { AverageExclusiveMs = 20 } : metric).ToArray() };
        vm.Refresh();
        Assert.Same(second, vm.Scopes[0]);
        vm.IsFrozen = true;
        Assert.Same(first, vm.Scopes[0]);
        Assert.Equal("Exclusive ms ↓", vm.ExclusiveHeading);
        vm.SortNameCommand.Execute(null);
        Assert.Equal("Scope ↑", vm.NameHeading);
    }

    [Fact]
    public void TrendFreezesWithDisplayAndClearsOnSessionChanges()
    {
        var profiling = new Profiling();
        var clock = new Clock();
        using var vm = new DiagnosticsWindowViewModel(profiling, clock);
        vm.Refresh();
        Assert.Single(vm.TrendSamples);
        clock.Seconds = 0.5;
        profiling.Recent = profiling.Recent with { LastFrame = 1, AverageFrameMs = 30 };
        vm.Refresh();
        Assert.Equal(2, vm.TrendSamples.Count);
        vm.IsFrozen = true;
        var exported = vm.ExportJson();
        clock.Seconds = 1;
        profiling.Recent = profiling.Recent with { LastFrame = 2, AverageFrameMs = 40 };
        vm.Refresh();
        Assert.Equal(2, vm.TrendSamples.Count);
        Assert.Equal(exported, vm.ExportJson());
        vm.IsFrozen = false;
        Assert.Equal(3, vm.TrendSamples.Count);
        profiling.Recent = profiling.Recent with { SessionId = "456", CompletedFrames = 0, SampleFrames = 0 };
        vm.Refresh();
        Assert.Empty(vm.TrendSamples);
        Assert.False(vm.HasData);
    }

    [Fact]
    public void FirstValidSampleEstablishesCostOrderThenLiveUpdatesKeepIt()
    {
        var profiling = new Profiling { Recent = Snapshot() with { SampleFrames = 0, CompletedFrames = 0 } };
        using var vm = new DiagnosticsWindowViewModel(profiling);
        vm.Refresh();
        profiling.Recent = Snapshot() with { Metrics = Snapshot().Metrics.Select(metric => metric.Name == "Symbols.Eye.Update" ? metric with { AverageExclusiveMs = 10 } : metric).ToArray() };
        vm.Refresh();
        Assert.Equal("Symbols.Eye.Update", vm.Scopes[0].Name);
        profiling.Recent = Snapshot();
        vm.Refresh();
        Assert.Equal("Symbols.Eye.Update", vm.Scopes[0].Name);
    }

    [Fact]
    public void EditorModuleResolvesViewModelWithDefaultClock()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new Profiling()).As<IEngineProfilingService>();
        builder.RegisterModule<DiagnosticsModule>();
        using var container = builder.Build();
        var vm = container.Resolve<DiagnosticsWindowViewModel>();
        vm.Refresh();
        Assert.Single(vm.TrendSamples);
    }
}
