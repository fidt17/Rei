using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project asset item selection callbacks, rename normalization, action routing, and highlight lifecycle.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Project")]
public sealed class ProjectAssetItemViewModelTests : IDisposable
{
    private sealed class TestFileExplorerProvider : ReiEditor.Models.Services.FileSystem.IFileExplorerProvider
    {
        public List<string> SelectedPaths { get; } = new();
        public void OpenDirectory(string directoryPath) { }
        public void OpenAndSelect(string path) => SelectedPaths.Add(path);
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();
    private readonly List<ProjectAssetItemViewModel> _items = new();

    /// <summary>
    /// Releases item VMs and isolated files.
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
    /// File rename initializes extensionless edit value, trims input, and restores original extension for callback.
    /// </summary>
    [AvaloniaFact]
    public void TestFileRenameNormalizesEditableAndFinalNames()
    {
        var renames = new List<string>();
        var item = TestCreateItem("Old.material", ProjectAssetType.Asset, rename: (_, value) => renames.Add(value));

        item.StartRenameCommand.Execute(null);
        Assert.Equal("Old", item.RenameValue.Value);
        item.RenameValue.Value = "  New  ";
        item.ConfirmRenameCommand.Execute(null);

        Assert.Equal(new[] { "New.material" }, renames);
    }

    /// <summary>
    /// Directory rename keeps full name, while whitespace confirmation is ignored.
    /// </summary>
    [AvaloniaFact]
    public void TestDirectoryRenamePreservesNameAndRejectsBlankInput()
    {
        var renames = new List<string>();
        var item = TestCreateItem("Folder", ProjectAssetType.Directory, rename: (_, value) => renames.Add(value));

        item.StartRenameCommand.Execute(null);
        Assert.Equal("Folder", item.RenameValue.Value);
        item.RenameValue.Value = "   ";
        item.ConfirmRenameCommand.Execute(null);

        Assert.Empty(renames);
    }

    /// <summary>
    /// Selection requests preserve modifiers, context selection identifies item, and action commands route once.
    /// </summary>
    [AvaloniaFact]
    public void TestSelectionAndActionCallbacksReceiveSourceItem()
    {
        var selections = new List<(ProjectAssetItemViewModel Item, KeyModifiers Modifiers)>();
        var contexts = new List<ProjectAssetItemViewModel>();
        var actions = new List<string>();
        var item = TestCreateItem(
            "Asset.rei",
            ProjectAssetType.Asset,
            select: (source, modifiers) => selections.Add((source, modifiers)),
            context: contexts.Add,
            delete: _ => actions.Add("delete"),
            duplicate: _ => actions.Add("duplicate"),
            move: _ => actions.Add("move"),
            open: _ => actions.Add("open"));

        item.RequestSelection(KeyModifiers.Control | KeyModifiers.Shift);
        item.RequestContextMenuSelection();
        item.DeleteCommand.Execute(null);
        item.DuplicateCommand.Execute(null);
        item.MoveCommand.Execute(null);
        item.OpenCommand.Execute(null);

        var selection = Assert.Single(selections);
        Assert.Same(item, selection.Item);
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, selection.Modifiers);
        Assert.Same(item, Assert.Single(contexts));
        Assert.Equal(new[] { "delete", "duplicate", "move", "open" }, actions);
    }

    /// <summary>
    /// Highlight and clear expose deterministic visual state without waiting on pulse timers.
    /// </summary>
    [AvaloniaFact]
    public void TestHighlightAndClearUpdateVisualState()
    {
        var item = TestCreateItem("Asset.rei", ProjectAssetType.Asset);
        item.Highlight();
        Assert.True(item.Highlighted.Value);

        item.ClearHighlight();
        Assert.False(item.Highlighted.Value);
    }

    /// <summary>
    /// Creates item with configurable callbacks and records it for cleanup.
    /// </summary>
    private ProjectAssetItemViewModel TestCreateItem(
        string name,
        ProjectAssetType type,
        Action<ProjectAssetItemViewModel, KeyModifiers>? select = null,
        Action<ProjectAssetItemViewModel>? context = null,
        Action<ProjectAssetItemViewModel>? delete = null,
        Action<ProjectAssetItemViewModel>? duplicate = null,
        Action<ProjectAssetItemViewModel, string>? rename = null,
        Action<ProjectAssetItemViewModel>? move = null,
        Action<ProjectAssetItemViewModel>? open = null)
    {
        var fullPath = _temporaryDirectory.GetPath(name);
        var actions = new ProjectAssetItemActions(
            select ?? ((_, _) => { }),
            context ?? (_ => { }),
            delete ?? (_ => { }),
            duplicate ?? (_ => { }),
            rename ?? ((_, _) => { }),
            move ?? (_ => { }),
            open ?? (_ => { }));
        var item = new ProjectAssetItemViewModel(
            name, fullPath, type, "asset-id", actions, new ContextMenuViewModel(), new TestFileExplorerProvider());
        _items.Add(item);
        return item;
    }
}
