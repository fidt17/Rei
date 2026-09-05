using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Project;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project window refresh rebuilding and event detachment during disposal.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Project")]
public sealed class ProjectWindowViewModelLifecycleTests : IDisposable
{
    private sealed class TestResourceService(string projectPath) : IResourceService
    {
        public string GetRootPath(params string[] path) => Path.Combine(new[] { projectPath }.Concat(path).ToArray());
        public string GetProjectPath(params string[] path) => Path.Combine(new[] { projectPath }.Concat(path).ToArray());
        public string GetScriptsPath(params string[] path) => Path.Combine(new[] { projectPath, "Scripts" }.Concat(path).ToArray());
        public IEnumerable<string> GetAllWithExtension(string extension) => Array.Empty<string>();
        public void CopyFilesRecursively(string source, string target) => throw new NotSupportedException();
        public void MoveFilesRecursively(string source, string target) => throw new NotSupportedException();
        public Task<T> Load<T>(string fullPath) => throw new NotSupportedException();
        public Task<T?> TryLoad<T>(string fullPath) => throw new NotSupportedException();
        public Task<bool> Write(string data, string fullPath) => throw new NotSupportedException();
        public bool Exists(string fullPath) => File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    private sealed class TestAssetSearchService : IAssetSearchService
    {
        public IReadOnlyList<AssetSearchResult> Search(string query) => Array.Empty<AssetSearchResult>();
        public IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions) => Array.Empty<AssetSearchResult>();
    }

    private sealed class TestEditorRefreshService : IEditorRefreshService
    {
        public event Action? RefreshedEvent;
        public void NotifyRefreshed() => RefreshedEvent?.Invoke();
    }

    private sealed class TestFileExplorerProvider : ReiEditor.Models.Services.FileSystem.IFileExplorerProvider
    {
        public void OpenDirectory(string directoryPath) { }
        public void OpenAndSelect(string path) { }
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();

    /// <summary>
    /// Releases isolated project directory.
    /// </summary>
    public void Dispose() => _temporaryDirectory.Dispose();

    /// <summary>
    /// Refresh rebuilds visible assets, then disposal clears state and prevents refresh/focus events from repopulating it.
    /// </summary>
    [AvaloniaFact]
    public async Task TestRefreshAndDisposeRespectProjectWindowLifecycle()
    {
        var root = _temporaryDirectory.GetPath("Project");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "First.asset"), "first");
        var refresh = new TestEditorRefreshService();
        var focus = new ProjectAssetFocusService();
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        var vm = new ProjectWindowViewModel(
            new TestResourceService(root),
            null!,
            registry,
            null!,
            null!,
            null!,
            null!,
            null!,
            new TestFileExplorerProvider(),
            new TestAssetSearchService(),
            refresh,
            null!,
            null!,
            null!,
            null!,
            null!,
            new SelectionService(new TestEntityApi()),
            focus,
            null!);
        Assert.Equal("First.asset", Assert.Single(vm.ActiveItems).Name.Value);
        File.WriteAllText(Path.Combine(root, "Second.asset"), "second");

        refresh.NotifyRefreshed();
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Equal(new[] { "First.asset", "Second.asset" }, vm.ActiveItems.Select(item => item.Name.Value));

        vm.Dispose();
        File.WriteAllText(Path.Combine(root, "Third.asset"), "third");
        refresh.NotifyRefreshed();
        focus.FocusAssetPath(Path.Combine(root, "First.asset"));
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.Empty(vm.ActiveItems);
        Assert.Empty(vm.RootDirectories);
        Assert.Empty(vm.PathSegments);
        Assert.Equal("", vm.ActiveDirectoryPath.Value);
    }
}
