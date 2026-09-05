using ReiEditor.Models.Resources.Client;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies directory tree discovery, selection fallback, ancestor expansion, and breadcrumb navigation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Project")]
public sealed class ProjectDirectoryBrowserTests : IDisposable
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

    private readonly TemporaryDirectory _temporaryDirectory = new();

    /// <summary>
    /// Releases isolated directory tree.
    /// </summary>
    public void Dispose() => _temporaryDirectory.Dispose();

    /// <summary>
    /// Build excludes generated script directories and selects existing preferred path with matching breadcrumbs.
    /// </summary>
    [Fact]
    public void TestBuildTreeUsesPreferredPathAndExcludesHiddenDirectories()
    {
        var root = _temporaryDirectory.GetPath("Project");
        var nested = Path.Combine(root, "Assets", "Textures");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "bin"));
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "crash_reports"));
        var selected = new List<string>();
        var browser = new ProjectDirectoryBrowser(() => false, () => { }, selected.Add);

        browser.BuildTree(new TestResourceService(root), nested);

        Assert.Equal(nested, browser.ActiveDirectoryPath.Value);
        Assert.Equal(nested, browser.SelectedDirectoryPath);
        Assert.Equal(new[] { "Project", "Assets", "Textures" }, browser.PathSegments.Select(segment => segment.Name));
        var scripts = Assert.Single(Assert.Single(browser.RootDirectories).ChildNodes, node => node.Name.Value == "Scripts");
        Assert.Empty(scripts.ChildNodes);
        Assert.Equal(new[] { nested }, selected);
        browser.Reset();
    }

    /// <summary>
    /// Missing preferred path falls back to root and missing open target leaves active directory unchanged.
    /// </summary>
    [Fact]
    public void TestMissingPathsFallBackWithoutFalseSelection()
    {
        var root = _temporaryDirectory.GetPath("Project");
        Directory.CreateDirectory(root);
        var browser = new ProjectDirectoryBrowser(() => false, () => { }, _ => { });

        browser.BuildTree(new TestResourceService(root), Path.Combine(root, "Missing"));
        browser.OpenDirectory(Path.Combine(root, "StillMissing"));

        Assert.Equal(root, browser.ActiveDirectoryPath.Value);
        Assert.Equal(root, browser.SelectedDirectoryPath);
        Assert.Single(browser.PathSegments);
        browser.Reset();
    }

    /// <summary>
    /// Opening nested directory expands ancestors, clears active search once, and breadcrumb command navigates upward.
    /// </summary>
    [Fact]
    public void TestOpenDirectoryExpandsAncestorsResetsSearchAndBreadcrumbNavigates()
    {
        var root = _temporaryDirectory.GetPath("Project");
        var assets = Path.Combine(root, "Assets");
        var nested = Path.Combine(assets, "Textures");
        Directory.CreateDirectory(nested);
        var hasSearch = false;
        var resetCount = 0;
        var browser = new ProjectDirectoryBrowser(() => hasSearch, () =>
        {
            resetCount++;
            hasSearch = false;
        }, _ => { });
        browser.BuildTree(new TestResourceService(root));
        hasSearch = true;

        browser.OpenDirectory(nested);

        var rootNode = Assert.Single(browser.RootDirectories);
        var assetsNode = Assert.Single(rootNode.ChildNodes);
        Assert.True(rootNode.Expanded.Value);
        Assert.True(assetsNode.Expanded.Value);
        Assert.Equal(1, resetCount);

        browser.PathSegments[1].NavigateCommand.Execute(null);

        Assert.Equal(assets, browser.ActiveDirectoryPath.Value);
        Assert.Equal(new[] { "Project", "Assets" }, browser.PathSegments.Select(segment => segment.Name));
        browser.Reset();
    }

    /// <summary>
    /// Missing project root yields empty tree, path, selection, and breadcrumbs.
    /// </summary>
    [Fact]
    public void TestMissingRootBuildsEmptyBrowserState()
    {
        var missing = _temporaryDirectory.GetPath("MissingProject");
        var browser = new ProjectDirectoryBrowser(() => false, () => { }, _ => throw new InvalidOperationException());

        browser.BuildTree(new TestResourceService(missing));

        Assert.Empty(browser.RootDirectories);
        Assert.Empty(browser.PathSegments);
        Assert.Equal("", browser.ActiveDirectoryPath.Value);
        Assert.Null(browser.SelectedDirectoryPath);
    }
}
