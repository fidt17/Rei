using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.EditorSetup;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Engine.Settings;

/// <summary>
/// Verifies engine settings loading, derived paths, reloads, and subscription lifetime.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "EngineSettings")]
public sealed class EngineSettingsProviderTests
{
    private sealed class TestPreferencesService : IEditorPreferencesService
    {
        public int GetEnginePathCalls { get; private set; }
        public string? EnginePath { get; set; }
        public Task InitializeAsync() => Task.CompletedTask;
        public string? GetEnginePath() { GetEnginePathCalls++; return EnginePath; }
        public void SetEnginePath(string path) => throw new InvalidOperationException();
        public string? GetMsBuildPath() => throw new InvalidOperationException();
        public void SetMsBuildPath(string path) => throw new InvalidOperationException();
        public string? GetTextEditorPath() => throw new InvalidOperationException();
        public void SetTextEditorPath(string path) => throw new InvalidOperationException();
        public IEnumerable<Project> GetBookmarkedProjects() => throw new InvalidOperationException();
        public void SetBookmarkedProjects(IEnumerable<Project> paths) => throw new InvalidOperationException();
        public ConsolePreferences GetConsolePreferences() => throw new InvalidOperationException();
        public void SetConsolePreferences(ConsolePreferences consolePreferences) => throw new InvalidOperationException();
        public ViewportGridSettings GetGridSettings() => throw new InvalidOperationException();
        public void SetGridSettings(ViewportGridSettings settings) => throw new InvalidOperationException();
        public string GetWindowContainerActiveTab(string tag) => throw new InvalidOperationException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new InvalidOperationException();
    }

    private sealed class TestEditorSettingsService : IEditorSettingsService
    {
        public event Action<bool>? EditorConfigurationChangedEvent
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }
        public event Action? ConfigurationSetEvent;
        public bool IsValid { get; set; }

        public void PublishConfigurationSet() => ConfigurationSetEvent?.Invoke();
        public Task InitializeAsync() => Task.CompletedTask;
        public bool IsEditorConfigurationValid() => IsValid;
        public void SaveConfiguration() => throw new InvalidOperationException();
        public bool IsEngineLocationValid() => throw new InvalidOperationException();
        public bool SetEngineLocation(string path) => throw new InvalidOperationException();
        public string GetEngineLocation() => throw new InvalidOperationException();
        public bool IsMsBuildLocationValid() => throw new InvalidOperationException();
        public bool SetMsBuildLocation(string path) => throw new InvalidOperationException();
        public string GetMsBuildLocation() => throw new InvalidOperationException();
        public bool IsTextEditorLocationValid() => throw new InvalidOperationException();
        public bool SetTextEditorLocation(string path) => throw new InvalidOperationException();
        public void ClearTextEditorLocation() => throw new InvalidOperationException();
        public string GetTextEditorLocation() => throw new InvalidOperationException();
    }

    /// <summary>
    /// Valid configuration loads version and derives every engine directory from settings file location.
    /// </summary>
    [Fact]
    public async Task InitializeLoadsSettingsAndDerivesPaths()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.RootPath;
        var settingsPath = directory.GetPath("engine.json");
        await WriteSettings(settingsPath, "1.2.3", "\\debug", "\\release", "\\src1;\\src2", "\\resources");
        var preferences = new TestPreferencesService { EnginePath = settingsPath };
        var editorSettings = new TestEditorSettingsService { IsValid = true };
        using var provider = CreateProvider(preferences, editorSettings);

        await provider.InitializeAsync();

        Assert.Equal(root, provider.GetEnginePath());
        Assert.Equal(root + "\\debug", provider.GetEngineDebugIncludeDir());
        Assert.Equal(root + "\\release", provider.GetEngineReleaseIncludeDir());
        Assert.Equal(";" + root + "\\src1;" + root + "\\src2", provider.GetEngineSourceIncludes());
        Assert.Equal(root + "\\resources", provider.GetEngineResourcesDir());
        Assert.Equal(root + "\\resources\\rei_behaviours", provider.GetEngineBehavioursDir());
        Assert.Equal("1.2.3", provider.GetEngineVersion());
    }

    /// <summary>
    /// Invalid editor configuration skips file access during initialization.
    /// </summary>
    [Fact]
    public async Task InitializeInvalidConfigurationSkipsLoad()
    {
        var preferences = new TestPreferencesService { EnginePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json") };
        var editorSettings = new TestEditorSettingsService { IsValid = false };
        using var provider = CreateProvider(preferences, editorSettings);

        await provider.InitializeAsync();

        Assert.Equal(0, preferences.GetEnginePathCalls);
        Assert.Null(provider.GetEnginePath());
    }

    /// <summary>
    /// Configuration-set event reloads engine file selected by current preferences.
    /// </summary>
    [Fact]
    public async Task ConfigurationSetReloadsCurrentEngineSettings()
    {
        using var directory = new TemporaryDirectory();
        var firstPath = directory.GetPath("first.json");
        var secondDirectory = directory.GetPath("second");
        Directory.CreateDirectory(secondDirectory);
        var secondPath = Path.Combine(secondDirectory, "engine.json");
        await WriteSettings(firstPath, "1", "\\d1", "", "", "");
        await WriteSettings(secondPath, "2", "\\d2", "", "", "");
        var preferences = new TestPreferencesService { EnginePath = firstPath };
        var editorSettings = new TestEditorSettingsService { IsValid = true };
        using var provider = CreateProvider(preferences, editorSettings);
        await provider.InitializeAsync();

        preferences.EnginePath = secondPath;
        editorSettings.PublishConfigurationSet();

        Assert.Equal(secondDirectory, provider.GetEnginePath());
        Assert.Equal("2", provider.GetEngineVersion());
        Assert.Equal(secondDirectory + "\\d2", provider.GetEngineDebugIncludeDir());
    }

    /// <summary>
    /// Failed reload preserves last successfully loaded settings.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"EngineVersion\":\" \"}")]
    public async Task FailedReloadPreservesPreviousSettings(string invalidSettings)
    {
        using var directory = new TemporaryDirectory();
        var root = directory.RootPath;
        var validPath = directory.GetPath("valid.json");
        var invalidPath = directory.GetPath("invalid.json");
        await WriteSettings(validPath, "1", "\\debug", "", "", "");
        await File.WriteAllTextAsync(invalidPath, invalidSettings);
        var preferences = new TestPreferencesService { EnginePath = validPath };
        var editorSettings = new TestEditorSettingsService { IsValid = true };
        var logger = new TestLogger<EngineSettingsProvider>();
        using var provider = CreateProvider(preferences, editorSettings, logger);
        await provider.InitializeAsync();

        preferences.EnginePath = invalidPath;
        editorSettings.PublishConfigurationSet();

        Assert.Equal(root, provider.GetEnginePath());
        Assert.Equal("1", provider.GetEngineVersion());
        Assert.Equal(root + "\\debug", provider.GetEngineDebugIncludeDir());
        Assert.Contains(logger.Entries, entry => entry.Exception != null);
    }

    /// <summary>
    /// Dispose unsubscribes configuration reload handler.
    /// </summary>
    [Fact]
    public async Task DisposeStopsConfigurationReloads()
    {
        using var directory = new TemporaryDirectory();
        var firstPath = directory.GetPath("first.json");
        var secondPath = directory.GetPath("second.json");
        await WriteSettings(firstPath, "1", "", "", "", "");
        await WriteSettings(secondPath, "2", "", "", "", "");
        var preferences = new TestPreferencesService { EnginePath = firstPath };
        var editorSettings = new TestEditorSettingsService { IsValid = true };
        var provider = CreateProvider(preferences, editorSettings);
        await provider.InitializeAsync();

        provider.Dispose();
        preferences.EnginePath = secondPath;
        editorSettings.PublishConfigurationSet();

        Assert.Equal("1", provider.GetEngineVersion());
    }

    private static EngineSettingsProvider CreateProvider(IEditorPreferencesService preferences, IEditorSettingsService editorSettings, TestLogger<EngineSettingsProvider>? logger = null)
        => new(preferences, new JsonSerializer(), logger ?? new TestLogger<EngineSettingsProvider>(), editorSettings);

    private static Task WriteSettings(string path, string version, string debug, string release, string includes, string resources)
    {
        var settings = new EngineSettings
        {
            EngineVersion = version,
            RelativeDebugIncludeDir = debug,
            RelativeReleaseIncludeDir = release,
            RelativeSourceIncludes = includes,
            RelativeResourcesDir = resources
        };
        return File.WriteAllTextAsync(path, new JsonSerializer().Serialize(settings));
    }
}
