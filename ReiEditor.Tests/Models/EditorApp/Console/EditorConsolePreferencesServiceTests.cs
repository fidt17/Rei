using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.Services.Preferences;
using EditorProject = ReiEditor.Models.ProjectManagement.Project;

namespace ReiEditor.Tests.Models.EditorApp.Console;

/// <summary>
/// Verifies console preference reads and change persistence.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Preferences")]
public sealed class EditorConsolePreferencesServiceTests
{
    private sealed class TestPreferencesService(ConsolePreferences consolePreferences) : IEditorPreferencesService
    {
        public List<ConsolePreferences> SavedConsolePreferences { get; } = new();
        public Task InitializeAsync() => Task.CompletedTask;
        public ConsolePreferences GetConsolePreferences() => consolePreferences;
        public void SetConsolePreferences(ConsolePreferences value) => SavedConsolePreferences.Add(value);
        public string? GetEnginePath() => throw new InvalidOperationException();
        public void SetEnginePath(string path) => throw new InvalidOperationException();
        public string? GetMsBuildPath() => throw new InvalidOperationException();
        public void SetMsBuildPath(string path) => throw new InvalidOperationException();
        public string? GetTextEditorPath() => throw new InvalidOperationException();
        public void SetTextEditorPath(string path) => throw new InvalidOperationException();
        public IEnumerable<EditorProject> GetBookmarkedProjects() => throw new InvalidOperationException();
        public void SetBookmarkedProjects(IEnumerable<EditorProject> paths) => throw new InvalidOperationException();
        public ViewportGridSettings GetGridSettings() => throw new InvalidOperationException();
        public void SetGridSettings(ViewportGridSettings settings) => throw new InvalidOperationException();
        public string GetWindowContainerActiveTab(string tag) => throw new InvalidOperationException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new InvalidOperationException();
    }

    /// <summary>
    /// Service reflects initial console preference values.
    /// </summary>
    [Fact]
    public void ReadsInitialPreferenceValues()
    {
        var service = new EditorConsolePreferencesService(new TestPreferencesService(new ConsolePreferences
        {
            DisplayDebugLogs = false,
            DisplayInfoLogs = true,
            DisplayWarningLogs = false,
            DisplayErrorLogs = true
        }));

        Assert.False(service.DebugEnabled());
        Assert.True(service.InfoEnabled());
        Assert.False(service.WarningEnabled());
        Assert.True(service.ErrorEnabled());
    }

    /// <summary>
    /// Changed levels persist shared preference object and immediately affect reads.
    /// </summary>
    [Fact]
    public void ChangedLevelsPersistAndUpdateReads()
    {
        var preferences = new TestPreferencesService(new ConsolePreferences());
        var service = new EditorConsolePreferencesService(preferences);

        service.SetDebug(false);
        service.SetInfo(false);
        service.SetWarning(false);
        service.SetError(false);

        Assert.False(service.DebugEnabled());
        Assert.False(service.InfoEnabled());
        Assert.False(service.WarningEnabled());
        Assert.False(service.ErrorEnabled());
        Assert.Equal(4, preferences.SavedConsolePreferences.Count);
        Assert.All(preferences.SavedConsolePreferences, saved => Assert.Same(preferences.SavedConsolePreferences[0], saved));
    }

    /// <summary>
    /// Assigning current values performs no preference writes.
    /// </summary>
    [Fact]
    public void UnchangedLevelsDoNotPersist()
    {
        var preferences = new TestPreferencesService(new ConsolePreferences());
        var service = new EditorConsolePreferencesService(preferences);

        service.SetDebug(true);
        service.SetInfo(true);
        service.SetWarning(true);
        service.SetError(true);

        Assert.Empty(preferences.SavedConsolePreferences);
    }
}
