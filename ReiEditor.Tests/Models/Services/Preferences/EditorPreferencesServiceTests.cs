using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Preferences;

/// <summary>
/// Verifies preference defaults, persistence, and invalid bookmark cleanup.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Preferences")]
public sealed class EditorPreferencesServiceTests
{
    /// <summary>
    /// Missing preferences create defaults and persist them to isolated storage.
    /// </summary>
    [Fact]
    public async Task InitializeMissingPreferencesPersistsDefaults()
    {
        var writes = new List<(string FileName, string Value)>();
        var storage = new TestEditorStorageService(
            _ => Task.FromResult<string?>(null),
            (fileName, value) =>
            {
                writes.Add((fileName, value));
                return Task.FromResult(true);
            });
        var service = new EditorPreferencesService(storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());

        await service.InitializeAsync();

        var write = Assert.Single(writes);
        Assert.Equal("preferences.json", write.FileName);
        var saved = JObject.Parse(write.Value);
        Assert.Equal("", saved[nameof(EditorPreferences.EnginePath)]?.Value<string>());
        Assert.True(service.GetGridSettings().RenderXZ);
        Assert.True(service.GetConsolePreferences().DisplayErrorLogs);
        Assert.Equal("", service.GetWindowContainerActiveTab("missing"));
    }

    /// <summary>
    /// Path, grid, console, and active-tab changes persist each updated value.
    /// </summary>
    [Fact]
    public async Task SettersPersistUpdatedPreferences()
    {
        var writes = new List<string>();
        var storage = new TestEditorStorageService(
            _ => Task.FromResult<string?>("{}"),
            (_, value) =>
            {
                writes.Add(value);
                return Task.FromResult(true);
            });
        var service = new EditorPreferencesService(storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        await service.InitializeAsync();
        var console = new ConsolePreferences { DisplayDebugLogs = false };
        var grid = new ViewportGridSettings { RenderXZ = false, RenderXY = true, Opacity = 0.75f };

        service.SetEnginePath("engine.json");
        service.SetMsBuildPath("MSBuild.exe");
        service.SetTextEditorPath("editor.exe");
        service.SetConsolePreferences(console);
        service.SetGridSettings(grid);
        service.SetWindowContainerActiveTab("left", "Assets");

        Assert.Equal(6, writes.Count);
        var saved = JObject.Parse(writes[^1]);
        Assert.Equal("engine.json", saved[nameof(EditorPreferences.EnginePath)]?.Value<string>());
        Assert.Equal("MSBuild.exe", saved[nameof(EditorPreferences.MsBuildPath)]?.Value<string>());
        Assert.Equal("editor.exe", saved[nameof(EditorPreferences.TextEditorPath)]?.Value<string>());
        Assert.False(saved[nameof(EditorPreferences.ConsolePreferences)]?[nameof(ConsolePreferences.DisplayDebugLogs)]?.Value<bool>());
        Assert.True(saved[nameof(EditorPreferences.GridSettings)]?[nameof(ViewportGridSettings.RenderXY)]?.Value<bool>());
        Assert.Equal("Assets", saved[nameof(EditorPreferences.WindowContainerActiveTabs)]?["left"]?.Value<string>());
        Assert.Same(console, service.GetConsolePreferences());
        Assert.Same(grid, service.GetGridSettings());
    }

    /// <summary>
    /// Empty tags and unchanged active tabs do not write preferences.
    /// </summary>
    [Fact]
    public async Task SetWindowContainerActiveTabSkipsInvalidAndUnchangedValues()
    {
        var writes = 0;
        var storage = new TestEditorStorageService(
            _ => Task.FromResult<string?>("{\"WindowContainerActiveTabs\":{\"left\":\"Assets\"}}"),
            (_, _) =>
            {
                writes++;
                return Task.FromResult(true);
            });
        var service = new EditorPreferencesService(storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        await service.InitializeAsync();

        service.SetWindowContainerActiveTab(" ", "Console");
        service.SetWindowContainerActiveTab("left", "Assets");

        Assert.Equal(0, writes);
    }

    /// <summary>
    /// Missing and malformed bookmark files are removed while valid projects are returned and persisted.
    /// </summary>
    [Fact]
    public async Task GetBookmarkedProjectsRemovesUnreadableEntriesAndPersistsCleanup()
    {
        using var directory = new TemporaryDirectory();
        var validPath = directory.GetPath("Valid.rei");
        var malformedPath = directory.GetPath("Malformed.rei");
        var missingPath = directory.GetPath("Missing.rei");
        await File.WriteAllTextAsync(validPath, "{\"ProjectName\":\"Valid\"}");
        await File.WriteAllTextAsync(malformedPath, "{");
        var initial = new JObject
        {
            [nameof(EditorPreferences.BookmarkedProjectsPaths)] = new JArray(validPath, malformedPath, missingPath)
        }.ToString();
        string? saved = null;
        var storage = new TestEditorStorageService(
            _ => Task.FromResult<string?>(initial),
            (_, value) =>
            {
                saved = value;
                return Task.FromResult(true);
            });
        var service = new EditorPreferencesService(storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        await service.InitializeAsync();

        var project = Assert.Single(service.GetBookmarkedProjects());

        Assert.Equal("Valid", project.ProjectName);
        Assert.Equal(Path.GetFullPath(validPath), project.ProjectFilePath);
        Assert.NotNull(saved);
        Assert.Equal(new[] { validPath }, JObject.Parse(saved!)[nameof(EditorPreferences.BookmarkedProjectsPaths)]!.Values<string>());
    }

    /// <summary>
    /// Malformed preference JSON fails initialization instead of silently replacing user data.
    /// </summary>
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task InitializeMalformedPreferencesThrows(string serializedPreferences)
    {
        var writes = 0;
        var storage = new TestEditorStorageService(
            _ => Task.FromResult<string?>(serializedPreferences),
            (_, _) =>
            {
                writes++;
                return Task.FromResult(true);
            });
        var service = new EditorPreferencesService(storage, new TestLogger<EditorPreferencesService>(), new JsonSerializer());

        await Assert.ThrowsAnyAsync<Exception>(() => service.InitializeAsync());
        Assert.Equal(0, writes);
    }
}
