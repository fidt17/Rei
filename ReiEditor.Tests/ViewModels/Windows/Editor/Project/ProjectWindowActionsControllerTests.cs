using Avalonia.Headless.XUnit;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project window action routing for directory and file opening.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Project")]
public sealed class ProjectWindowActionsControllerTests : IDisposable
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

    private sealed class TestFileExplorerProvider : IFileExplorerProvider
    {
        public void OpenDirectory(string directoryPath) { }
        public void OpenAndSelect(string path) { }
    }

    private sealed class TestTextEditorFileOpener : ITextEditorFileOpener
    {
        public List<string> OpenedPaths { get; } = new();
        public bool CanOpenWithTextEditor(string filePath) => true;
        public TextEditorOpenResult Open(string filePath)
        {
            OpenedPaths.Add(filePath);
            return TextEditorOpenResult.Opened;
        }
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();
    private readonly List<ProjectAssetItemViewModel> _items = new();

    /// <summary>
    /// Releases item VMs, browser tree, and isolated paths.
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _items)
        {
            item.Dispose();
        }

        _temporaryDirectory.Dispose();
    }

    /// <summary>
    /// Open action navigates known directories and sends regular files to text editor opener.
    /// </summary>
    [AvaloniaFact]
    public void TestOpenActionRoutesDirectoryAndFileTargets()
    {
        var root = _temporaryDirectory.GetPath("Project");
        var folder = Path.Combine(root, "Folder");
        var file = Path.Combine(root, "Asset.cpp");
        Directory.CreateDirectory(folder);
        File.WriteAllText(file, "code");
        var browser = new ProjectDirectoryBrowser(() => false, () => { }, _ => { });
        browser.BuildTree(new TestResourceService(root));
        var selection = new ProjectAssetSelectionHandler(null, _ => { });
        var operations = new ProjectAssetOperationsHandler(null, null, null, null, null, null);
        var opener = new TestTextEditorFileOpener();
        var controller = new ProjectWindowActionsController(
            browser, selection, operations, opener, null, () => _items, _ => { }, _ => { });
        var actions = controller.CreateAssetItemActions((_, _) => { }, _ => { });
        var explorer = new TestFileExplorerProvider();
        var directoryItem = new ProjectAssetItemViewModel(
            "Folder", folder, ProjectAssetType.Directory, "", actions, new ContextMenuViewModel(), explorer);
        var fileItem = new ProjectAssetItemViewModel(
            "Asset.cpp", file, ProjectAssetType.Script, "asset-id", actions, new ContextMenuViewModel(), explorer);
        _items.Add(directoryItem);
        _items.Add(fileItem);

        actions.OpenAction(directoryItem);
        actions.OpenAction(fileItem);

        Assert.Equal(folder, browser.ActiveDirectoryPath.Value);
        Assert.Equal(new[] { file }, opener.OpenedPaths);
        browser.Reset();
    }
}
