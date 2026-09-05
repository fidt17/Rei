using ReiEditor.Models.EditorApp.ViewportGrid;
using ReiEditor.Models.ProjectManagement;
using ReiEditor.Models.ProjectManagement.BookmarkedProjects;
using ReiEditor.Models.Services.Preferences;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.ProjectManagement;

/// <summary>
/// Verifies bookmark collection state, persistence, and notifications.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "ProjectManagement")]
public sealed class BookmarkedProjectsServiceTests
{
    private sealed class TestPreferencesService(IEnumerable<Project>? projects = null) : IEditorPreferencesService
    {
        public List<IReadOnlyList<Project>> SavedProjects { get; } = new();
        private readonly List<Project> _projects = projects?.ToList() ?? new();

        public Task InitializeAsync() => Task.CompletedTask;
        public IEnumerable<Project> GetBookmarkedProjects() => _projects;
        public void SetBookmarkedProjects(IEnumerable<Project> paths) => SavedProjects.Add(paths.ToList());
        public string? GetEnginePath() => throw new InvalidOperationException();
        public void SetEnginePath(string path) => throw new InvalidOperationException();
        public string? GetMsBuildPath() => throw new InvalidOperationException();
        public void SetMsBuildPath(string path) => throw new InvalidOperationException();
        public string? GetTextEditorPath() => throw new InvalidOperationException();
        public void SetTextEditorPath(string path) => throw new InvalidOperationException();
        public ConsolePreferences GetConsolePreferences() => throw new InvalidOperationException();
        public void SetConsolePreferences(ConsolePreferences consolePreferences) => throw new InvalidOperationException();
        public ViewportGridSettings GetGridSettings() => throw new InvalidOperationException();
        public void SetGridSettings(ViewportGridSettings settings) => throw new InvalidOperationException();
        public string GetWindowContainerActiveTab(string tag) => throw new InvalidOperationException();
        public void SetWindowContainerActiveTab(string tag, string tabName) => throw new InvalidOperationException();
    }

    /// <summary>
    /// Adding a new project persists snapshot and publishes collection change.
    /// </summary>
    [Fact]
    public void AddProjectPersistsSnapshotAndPublishesChange()
    {
        var preferences = new TestPreferencesService();
        var service = new BookmarkedProjectsService(preferences, new TestLogger<BookmarkedProjectsService>());
        var project = CreateProject("Game.rei");
        var notifications = 0;
        service.BookmarkedProjectsCollectionChangedEvent += () => notifications++;

        service.AddProject(project);

        Assert.Same(project, Assert.Single(service.GetBookmarkedProjects()));
        Assert.Same(project, Assert.Single(Assert.Single(preferences.SavedProjects)));
        Assert.Equal(1, notifications);
    }

    /// <summary>
    /// Adding different instance with same project path is ignored without persistence or notification.
    /// </summary>
    [Fact]
    public void AddEquivalentProjectIsIgnored()
    {
        var existing = CreateProject("Game.rei");
        var equivalent = new Project();
        equivalent.SetProjectFilePath(Path.Combine(Path.GetDirectoryName(existing.ProjectFilePath)!, ".", "Game.rei"));
        var preferences = new TestPreferencesService(new[] { existing });
        var service = new BookmarkedProjectsService(preferences, new TestLogger<BookmarkedProjectsService>());
        var notifications = 0;
        service.BookmarkedProjectsCollectionChangedEvent += () => notifications++;

        service.AddProject(equivalent);

        Assert.Same(existing, Assert.Single(service.GetBookmarkedProjects()));
        Assert.Empty(preferences.SavedProjects);
        Assert.Equal(0, notifications);
    }

    /// <summary>
    /// Removing stored instance persists remaining snapshot and publishes collection change.
    /// </summary>
    [Fact]
    public void RemoveStoredProjectPersistsRemainingSnapshotAndPublishesChange()
    {
        var first = CreateProject("First.rei");
        var second = CreateProject("Second.rei");
        var preferences = new TestPreferencesService(new[] { first, second });
        var service = new BookmarkedProjectsService(preferences, new TestLogger<BookmarkedProjectsService>());
        var notifications = 0;
        service.BookmarkedProjectsCollectionChangedEvent += () => notifications++;

        service.RemoveProject(first);

        Assert.Same(second, Assert.Single(service.GetBookmarkedProjects()));
        Assert.Same(second, Assert.Single(Assert.Single(preferences.SavedProjects)));
        Assert.Equal(1, notifications);
    }

    private static Project CreateProject(string fileName)
    {
        var project = new Project();
        project.SetProjectFilePath(Path.Combine(Path.GetTempPath(), "ReiEditorTests", Guid.NewGuid().ToString("N"), fileName));
        return project;
    }
}
