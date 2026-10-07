using ReiEditor.Models.Services.Scenes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using ReiEditor.Views.Windows.Editor.Project.Assets;
using Avalonia.Headless.XUnit;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;

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

    private sealed class TestSceneOpener : ISceneOpeningService
    {
        public List<string> OpenedIds { get; } = new();
        public Task<bool> OpenAsync(string assetId)
        {
            OpenedIds.Add(assetId);
            return Task.FromResult(true);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void SceneDoubleClickUsesSceneServiceAndNeverTextEditor(bool hasService)
    {
        var browser = new ProjectDirectoryBrowser(() => false, () => { }, _ => { });
        var opener = new TestTextEditorFileOpener();
        var scenes = new TestSceneOpener();
        var controller = new ProjectWindowActionsController(browser, new ProjectAssetSelectionHandler(null, _ => { }),
            new ProjectAssetOperationsHandler(null, null, null, null, null, null), opener, null, () => _items, _ => { }, _ => { }, hasService ? scenes : null);
        var path = _temporaryDirectory.GetPath("Level.scene");
        var item = new ProjectAssetItemViewModel("Level", path, ProjectAssetType.Scene,
            new AssetInfo(new AssetMeta("scene-id"), path), controller.CreateAssetItemActions((_, _) => { }, _ => { }),
            new ContextMenuViewModel(), new TestFileExplorerProvider());
        _items.Add(item);

        var theme = new FluentTheme();
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Add(styles);
        var window = new Window { Width = 400, Height = 120, Content = new ProjectAssetItemView { DataContext = item } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var point = new Point(80, 12);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
            Application.Current.Styles.Remove(styles);
            Application.Current.Styles.Remove(theme);
        }

        Assert.Equal(hasService ? new[] { "scene-id" } : Array.Empty<string>(), scenes.OpenedIds);
        Assert.Empty(opener.OpenedPaths);
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
            "Folder", folder, ProjectAssetType.Directory, null, actions, new ContextMenuViewModel(), explorer);
        var fileItem = new ProjectAssetItemViewModel(
            "Asset.cpp", file, ProjectAssetType.Script, new AssetInfo(new AssetMeta("asset-id"), file), actions, new ContextMenuViewModel(), explorer);
        _items.Add(directoryItem);
        _items.Add(fileItem);

        actions.OpenAction(directoryItem);
        actions.OpenAction(fileItem);

        Assert.Equal(folder, browser.ActiveDirectoryPath.Value);
        Assert.Equal(new[] { file }, opener.OpenedPaths);
        browser.Reset();
    }
}
