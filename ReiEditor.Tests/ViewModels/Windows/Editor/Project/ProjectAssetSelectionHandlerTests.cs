using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project asset selection ranges, primary selection, restoration, and command target resolution.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Project")]
public sealed class ProjectAssetSelectionHandlerTests : IDisposable
{
    private sealed class TestFileExplorerProvider : ReiEditor.Models.Services.FileSystem.IFileExplorerProvider
    {
        public void OpenDirectory(string directoryPath) { }
        public void OpenAndSelect(string path) { }
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();
    private readonly SelectionService _selectionService = new(new TestEntityApi());
    private readonly List<ProjectAssetItemViewModel> _items = new();

    /// <summary>
    /// Releases isolated filesystem and asset VMs.
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
    /// Plain, Ctrl, and reverse Shift clicks maintain inclusive selection and target primary asset.
    /// </summary>
    [AvaloniaFact]
    public void TestClickModifiersUpdateAssetSelectionAndPrimary()
    {
        var handler = new ProjectAssetSelectionHandler(_selectionService, _ => { });
        var items = TestCreateItems("One.asset", "Two.asset", "Three.asset", "Four.asset");

        handler.HandleSelectionRequested(items[3], KeyModifiers.None, items);
        handler.HandleSelectionRequested(items[1], KeyModifiers.Shift, items);

        Assert.False(items[0].Selected.Value);
        Assert.All(items.Skip(1), item => Assert.True(item.Selected.Value));
        Assert.Same(items[1], _selectionService.ActiveSelection.Value);

        handler.HandleSelectionRequested(items[0], KeyModifiers.Control | KeyModifiers.Shift, items);

        Assert.All(items, item => Assert.True(item.Selected.Value));
        Assert.Same(items[0], _selectionService.ActiveSelection.Value);
    }

    /// <summary>
    /// Context click preserves selected set and case-insensitive restore prunes paths absent from current view.
    /// </summary>
    [AvaloniaFact]
    public void TestContextSelectionAndRestoreAreCaseInsensitive()
    {
        var tracked = new List<ProjectAssetItemViewModel>();
        var handler = new ProjectAssetSelectionHandler(_selectionService, tracked.Add);
        var items = TestCreateItems("Alpha.asset", "Beta.asset", "Gamma.asset");
        handler.HandleSelectionRequested(items[0], KeyModifiers.None, items);
        handler.HandleSelectionRequested(items[2], KeyModifiers.Control, items);

        handler.HandleContextMenuSelectionRequested(items[0], items);

        Assert.True(items[0].Selected.Value);
        Assert.True(items[2].Selected.Value);
        Assert.Same(items[0], _selectionService.ActiveSelection.Value);

        handler.SetSelectionState(
            new[] { items[1].FullPath.ToUpperInvariant(), _temporaryDirectory.GetPath("gone.asset") },
            items[1].FullPath.ToUpperInvariant(),
            _temporaryDirectory.GetPath("gone.asset"));
        handler.RestoreSelection(items);

        Assert.False(items[0].Selected.Value);
        Assert.True(items[1].Selected.Value);
        Assert.False(items[2].Selected.Value);
        Assert.Same(items[1], _selectionService.ActiveSelection.Value);
    }

    /// <summary>
    /// Selected directory suppresses duplicate and descendant targets without suppressing similarly prefixed siblings.
    /// </summary>
    [AvaloniaFact]
    public void TestResolveCommandTargetsCollapsesDirectoryDescendantsOnly()
    {
        var parent = _temporaryDirectory.GetPath("foo");
        var sibling = _temporaryDirectory.GetPath("foo2");
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(sibling);
        var child = Path.Combine(parent, "child.asset");
        var siblingChild = Path.Combine(sibling, "child.asset");
        File.WriteAllText(child, "child");
        File.WriteAllText(siblingChild, "sibling");
        var handler = new ProjectAssetSelectionHandler(null, _ => { });

        var targets = handler.ResolveCommandTargets(new[] { "", parent, parent.ToUpperInvariant(), child, siblingChild });

        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, target => target.IsDirectory && string.Equals(target.FullPath, parent, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(targets, target => !target.IsDirectory && target.FullPath == siblingChild);
    }

    /// <summary>
    /// Creates headless asset VMs rooted in test-owned temporary directory.
    /// </summary>
    private IReadOnlyList<ProjectAssetItemViewModel> TestCreateItems(params string[] names)
    {
        var actions = new ProjectAssetItemActions((_, _) => { }, _ => { }, _ => { }, _ => { }, (_, _) => { }, _ => { }, _ => { });
        foreach (var name in names)
        {
            var fullPath = _temporaryDirectory.GetPath(name);
            File.WriteAllText(fullPath, name);
            _items.Add(new ProjectAssetItemViewModel(
                name,
                fullPath,
                ProjectAssetType.Asset,
                name,
                actions,
                new ContextMenuViewModel(),
                new TestFileExplorerProvider()));
        }

        return _items.ToArray();
    }
}
