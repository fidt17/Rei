using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Console;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Console;

/// <summary>Verifies console filtering, detail selection, clearing and subscription ownership.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Console")]
public sealed class ConsoleViewModelTests
{
    private sealed class TestPreferences : IEditorConsolePreferencesService
    {
        public bool Debug { get; set; } = true;
        public bool Info { get; set; } = true;
        public bool Warning { get; set; } = true;
        public bool Error { get; set; } = true;
        public List<(LogLevelEnum Level, bool Enabled)> Writes { get; } = [];
        public bool DebugEnabled() => Debug;
        public bool InfoEnabled() => Info;
        public bool WarningEnabled() => Warning;
        public bool ErrorEnabled() => Error;
        public void SetDebug(bool enabled) { Debug = enabled; Writes.Add((LogLevelEnum.Debug, enabled)); }
        public void SetInfo(bool enabled) { Info = enabled; Writes.Add((LogLevelEnum.Info, enabled)); }
        public void SetWarning(bool enabled) { Warning = enabled; Writes.Add((LogLevelEnum.Warning, enabled)); }
        public void SetError(bool enabled) { Error = enabled; Writes.Add((LogLevelEnum.Error, enabled)); }
    }

    /// <summary>Each level toggle persists and publishes once; filtering keeps allowed messages in input order.</summary>
    [Theory]
    [InlineData(LogLevelEnum.Debug)]
    [InlineData(LogLevelEnum.Info)]
    [InlineData(LogLevelEnum.Warning)]
    [InlineData(LogLevelEnum.Error)]
    public void FilterTogglesPersistOnlyChanges(LogLevelEnum disabled)
    {
        var preferences = new TestPreferences();
        using var vm = new ConsoleFilterViewModel(preferences);
        var changed = 0;
        vm.FilterChangedEvent += () => changed++;
        void Disable()
        {
            switch (disabled)
            {
                case LogLevelEnum.Debug: vm.DebugEnabled = false; break;
                case LogLevelEnum.Info: vm.InfoEnabled = false; break;
                case LogLevelEnum.Warning: vm.WarningEnabled = false; break;
                case LogLevelEnum.Error: vm.ErrorEnabled = false; break;
            }
        }
        Disable();
        Disable();
        var logs = new[] { Message(LogLevelEnum.Error), Message(LogLevelEnum.Debug), Message(LogLevelEnum.Warning), Message(LogLevelEnum.Info) };
        Assert.Equal(logs.Where(x => x.Level != disabled), vm.FilterMessages(logs));
        Assert.False(vm.IsValidLog(Message((LogLevelEnum)999)));
        Assert.Equal(1, changed);
        Assert.Equal((disabled, false), Assert.Single(preferences.Writes));
    }

    /// <summary>Persisted flags initialize filtering before any user toggle.</summary>
    [Fact]
    public void FilterLoadsPersistedFlags()
    {
        using var vm = new ConsoleFilterViewModel(new TestPreferences { Debug = false, Info = false, Warning = true, Error = false });
        Assert.False(vm.DebugEnabled);
        Assert.False(vm.InfoEnabled);
        Assert.True(vm.WarningEnabled);
        Assert.False(vm.ErrorEnabled);
        Assert.Equal(LogLevelEnum.Warning, Assert.Single(vm.FilterMessages([Message(LogLevelEnum.Warning), Message(LogLevelEnum.Error)])).Level);
    }

    /// <summary>Log view models format timestamps and publish expansion only when changing to expanded state.</summary>
    [Fact]
    public void LogExpansionPublishesOnceAndDisablesCommand()
    {
        using var vm = new ConsoleLogMessageViewModel(Message(LogLevelEnum.Warning));
        var expanded = new List<ConsoleLogMessageViewModel>();
        vm.DetailsExpandedEvent += expanded.Add;
        Assert.Equal("03:04:05", vm.Time);
        Assert.Equal(LogLevelEnum.Warning, vm.LogLevel);
        Assert.Contains("detail", vm.Details);
        Assert.True(vm.ExpandContentsCommand.CanExecute(null));
        vm.ExpandContentsCommand.Execute(null);
        vm.Expand = true;
        Assert.Same(vm, Assert.Single(expanded));
        Assert.False(vm.ExpandContentsCommand.CanExecute(null));
        vm.Expand = false;
        Assert.True(vm.ExpandContentsCommand.CanExecute(null));
    }

    /// <summary>Filtering rebuilds from console history; expanding one row collapses the previous row and clear removes details.</summary>
    [AvaloniaFact]
    public void ConsoleRebuildExpansionAndClearStayConsistent()
    {
        var console = new EditorConsoleService();
        using var vm = new ConsoleEditorWindowViewModel(console, new TestPreferences());
        Assert.False(vm.ClearEditorConsoleCommand.CanExecute(null));
        var updated = 0;
        vm.LogCollectionUpdated += () => updated++;
        console.Log(Message(LogLevelEnum.Info));
        console.Log(Message(LogLevelEnum.Error));
        Assert.Equal(2, updated);
        Assert.True(vm.ClearEditorConsoleCommand.CanExecute(null));
        var first = vm.FilteredLogs[0];
        var second = vm.FilteredLogs[1];
        first.Expand = true;
        second.Expand = true;
        Assert.False(first.Expand);
        Assert.True(second.Expand);
        Assert.True(vm.HasDetails);
        Assert.Contains("detail", vm.Details);
        vm.ConsoleFilter.InfoEnabled = false;
        Assert.Equal(LogLevelEnum.Error, Assert.Single(vm.FilteredLogs).LogLevel);
        Assert.False(vm.HasDetails);
        vm.ConsoleFilter.InfoEnabled = true;
        Assert.Equal(2, vm.FilteredLogs.Count);
        vm.ClearEditorConsoleCommand.Execute(null);
        Assert.Empty(vm.FilteredLogs);
        Assert.Empty(vm.Details);
        Assert.False(vm.HasDetails);
        Assert.False(vm.ClearEditorConsoleCommand.CanExecute(null));
    }

    /// <summary>Disposed console views must stop reacting to both appended and cleared logs.</summary>
    [AvaloniaFact]
    public void DisposeDetachesClearAndAppendSubscriptions()
    {
        var console = new EditorConsoleService();
        var vm = new ConsoleEditorWindowViewModel(console, new TestPreferences());
        console.Log(Message(LogLevelEnum.Info));
        vm.Dispose();
        var rows = vm.FilteredLogs.ToArray();
        console.Log(Message(LogLevelEnum.Error));
        Assert.Equal(rows, vm.FilteredLogs);
        console.ClearConsole();
        Assert.Equal(rows, vm.FilteredLogs);
    }

    private static LogMessage Message(LogLevelEnum level) => new(LogScopeEnum.Editor, level, new DateTime(2026, 1, 2, 3, 4, 5), "message", "detail");
}
