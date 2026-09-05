using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement;

/// <summary>
/// Verifies editor configuration initialization, validation, and state transitions.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "ProjectManagement")]
public sealed class EditorSettingsServiceTests
{
    private sealed class TestPreferencesService : IEditorPreferencesService
    {
        public string? EnginePath { get; set; }
        public string? MsBuildPath { get; set; }
        public string? TextEditorPath { get; set; }
        public int SetCalls { get; private set; }

        public Task InitializeAsync() => Task.CompletedTask;
        public string? GetEnginePath() => EnginePath;
        public void SetEnginePath(string path) { EnginePath = path; SetCalls++; }
        public string? GetMsBuildPath() => MsBuildPath;
        public void SetMsBuildPath(string path) { MsBuildPath = path; SetCalls++; }
        public string? GetTextEditorPath() => TextEditorPath;
        public void SetTextEditorPath(string path) { TextEditorPath = path; SetCalls++; }
        public IEnumerable<Project> GetBookmarkedProjects() => throw new InvalidOperationException();
        public void SetBookmarkedProjects(IEnumerable<Project> paths) => throw new InvalidOperationException();
        public ConsolePreferences GetConsolePreferences() => throw new InvalidOperationException();
        public void SetConsolePreferences(ConsolePreferences consolePreferences) => throw new InvalidOperationException();
        public ViewportGridSettings GetGridSettings() => throw new InvalidOperationException();
        public void SetGridSettings(ViewportGridSettings settings) => throw new InvalidOperationException();
        public string GetWindowContainerActiveTab(string tag) => throw new InvalidOperationException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new InvalidOperationException();
    }

    /// <summary>
    /// Initialization loads all configured paths from preferences.
    /// </summary>
    [Fact]
    public async Task InitializeLoadsConfiguredPaths()
    {
        var preferences = new TestPreferencesService
        {
            EnginePath = "engine.json",
            MsBuildPath = "MSBuild.exe",
            TextEditorPath = "editor.exe"
        };
        var service = CreateService(preferences);

        await service.InitializeAsync();

        Assert.Equal("engine.json", service.GetEngineLocation());
        Assert.Equal("MSBuild.exe", service.GetMsBuildLocation());
        Assert.Equal("editor.exe", service.GetTextEditorLocation());
    }

    /// <summary>
    /// Valid engine and text-editor files update state and publish current overall validity.
    /// </summary>
    [Fact]
    public async Task ValidFileSettersUpdateStateAndPublishValidity()
    {
        using var directory = new TemporaryDirectory();
        var enginePath = directory.GetPath("engine.json");
        var editorPath = directory.GetPath("editor.EXE");
        await File.WriteAllTextAsync(enginePath, "{\"EngineVersion\":\"1\"}");
        await File.WriteAllTextAsync(editorPath, "fixture");
        var service = CreateService(new TestPreferencesService());
        await service.InitializeAsync();
        var states = new List<bool>();
        service.EditorConfigurationChangedEvent += states.Add;

        Assert.True(service.SetEngineLocation(enginePath));
        Assert.True(service.SetTextEditorLocation(editorPath));

        Assert.Equal(enginePath, service.GetEngineLocation());
        Assert.Equal(editorPath, service.GetTextEditorLocation());
        Assert.Equal(new[] { false, false }, states);
        Assert.True(service.IsEngineLocationValid());
        Assert.True(service.IsTextEditorLocationValid());
    }

    /// <summary>
    /// Invalid replacement preserves prior engine path and emits no configuration event.
    /// </summary>
    [Fact]
    public async Task InvalidEngineReplacementPreservesPreviousValue()
    {
        using var directory = new TemporaryDirectory();
        var enginePath = directory.GetPath("engine.json");
        await File.WriteAllTextAsync(enginePath, "{}");
        var service = CreateService(new TestPreferencesService());
        await service.InitializeAsync();
        Assert.True(service.SetEngineLocation(enginePath));
        var notifications = 0;
        service.EditorConfigurationChangedEvent += _ => notifications++;

        Assert.False(service.SetEngineLocation(directory.GetPath("missing.json")));

        Assert.Equal(enginePath, service.GetEngineLocation());
        Assert.Equal(0, notifications);
    }

    /// <summary>
    /// Missing, wrongly named, and unversioned MSBuild files are rejected without changing state.
    /// </summary>
    [Fact]
    public async Task InvalidMsBuildFilesAreRejectedWithoutChangingState()
    {
        using var directory = new TemporaryDirectory();
        var wrongName = directory.GetPath("builder.exe");
        var unversionedMsBuild = directory.GetPath("MSBuild.exe");
        await File.WriteAllTextAsync(wrongName, "fixture");
        await File.WriteAllTextAsync(unversionedMsBuild, "fixture");
        var service = CreateService(new TestPreferencesService());
        await service.InitializeAsync();

        Assert.False(service.SetMsBuildLocation(directory.GetPath("missing", "MSBuild.exe")));
        Assert.False(service.SetMsBuildLocation(wrongName));
        Assert.False(service.SetMsBuildLocation(unversionedMsBuild));
        Assert.Equal("", service.GetMsBuildLocation());
    }

    /// <summary>
    /// Missing files and existing non-executables are rejected as text editors.
    /// </summary>
    [Fact]
    public async Task InvalidTextEditorFilesAreRejectedWithoutChangingState()
    {
        using var directory = new TemporaryDirectory();
        var textFile = directory.GetPath("editor.txt");
        await File.WriteAllTextAsync(textFile, "fixture");
        var service = CreateService(new TestPreferencesService());
        await service.InitializeAsync();

        Assert.False(service.SetTextEditorLocation(directory.GetPath("missing.exe")));
        Assert.False(service.SetTextEditorLocation(textFile));
        Assert.Equal("", service.GetTextEditorLocation());
    }

    /// <summary>
    /// Clearing optional text editor produces empty valid value and publishes current overall validity.
    /// </summary>
    [Fact]
    public async Task ClearTextEditorLocationProducesValidEmptyValueAndPublishesChange()
    {
        var service = CreateService(new TestPreferencesService { TextEditorPath = "missing.exe" });
        await service.InitializeAsync();
        bool? published = null;
        service.EditorConfigurationChangedEvent += value => published = value;

        service.ClearTextEditorLocation();

        Assert.Equal("", service.GetTextEditorLocation());
        Assert.True(service.IsTextEditorLocationValid());
        Assert.False(published);
    }

    /// <summary>
    /// Saving invalid configuration throws before writing any preference values or publishing completion.
    /// </summary>
    [Fact]
    public async Task SaveInvalidConfigurationDoesNotPersistOrPublishCompletion()
    {
        var preferences = new TestPreferencesService();
        var service = CreateService(preferences);
        await service.InitializeAsync();
        var completions = 0;
        service.ConfigurationSetEvent += () => completions++;

        Assert.Throws<Exception>(() => service.SaveConfiguration());

        Assert.Equal(0, preferences.SetCalls);
        Assert.Equal(0, completions);
    }

    private static EditorSettingsService CreateService(IEditorPreferencesService preferences)
        => new(preferences, new JsonSerializer(), new TestLogger<EditorSettingsService>());
}
