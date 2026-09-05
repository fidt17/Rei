using Avalonia.Headless.XUnit;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Windows.Editor.Project.Assets;
using ReiEditor.ViewModels.Windows.Editor.Project.Services;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Project;

/// <summary>
/// Verifies project item discovery, filtering, ordering, type classification, and registry metadata lookup.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Project")]
public sealed class ProjectAssetItemBuilderTests : IDisposable
{
    private sealed class TestAssetSearchService : IAssetSearchService
    {
        public IReadOnlyList<AssetSearchResult> Results { get; set; } = Array.Empty<AssetSearchResult>();
        public List<string> Queries { get; } = new();

        public IReadOnlyList<AssetSearchResult> Search(string query)
        {
            Queries.Add(query);
            return Results;
        }

        public IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestFileExplorerProvider : ReiEditor.Models.Services.FileSystem.IFileExplorerProvider
    {
        public void OpenDirectory(string directoryPath) { }
        public void OpenAndSelect(string path) { }
    }

    private readonly TemporaryDirectory _temporaryDirectory = new();
    private readonly List<ProjectAssetItemViewModel> _items = new();

    /// <summary>
    /// Releases built item VMs and isolated files.
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
    /// Directory build sorts folders before files, hides metadata/project files, classifies types, and resolves registered IDs.
    /// </summary>
    [AvaloniaFact]
    public void TestBuildDirectoryFiltersSortsAndClassifiesItems()
    {
        var root = _temporaryDirectory.GetPath("Project");
        Directory.CreateDirectory(Path.Combine(root, "ZFolder"));
        Directory.CreateDirectory(Path.Combine(root, "AFolder"));
        var scene = Path.Combine(root, "Level.scene");
        var script = Path.Combine(root, "Logic.cpp");
        var asset = Path.Combine(root, "Texture.png");
        File.WriteAllText(scene, "scene");
        File.WriteAllText(script, "script");
        File.WriteAllText(asset, "asset");
        File.WriteAllText(Path.Combine(root, "Texture.png.meta"), "meta");
        File.WriteAllText(Path.Combine(root, "Project.vcxproj"), "project");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta("texture-id"), asset) });
        var builder = new ProjectAssetItemBuilder(registry, null, new TestFileExplorerProvider());

        var items = builder.BuildItemsForDirectory(root, new ContextMenuViewModel(), TestActions());
        _items.AddRange(items);

        Assert.Equal(new[] { "AFolder", "ZFolder", "Level.scene", "Logic.cpp", "Texture.png" }, items.Select(item => item.Name.Value));
        Assert.Equal(ProjectAssetType.Directory, items[0].AssetType);
        Assert.Equal(ProjectAssetType.Scene, items[2].AssetType);
        Assert.Equal(ProjectAssetType.Script, items[3].AssetType);
        Assert.Equal(ProjectAssetType.Asset, items[4].AssetType);
        Assert.Equal("texture-id", items[4].AssetId);
        Assert.Empty(items[2].AssetId);
    }

    /// <summary>
    /// Search build preserves service order and maps directory and registered file metadata.
    /// </summary>
    [AvaloniaFact]
    public void TestBuildSearchPreservesResultOrderAndMetadata()
    {
        var directory = _temporaryDirectory.GetPath("Folder");
        var file = _temporaryDirectory.GetPath("Code.h");
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[] { new AssetInfo(new AssetMeta("code-id"), file) });
        var search = new TestAssetSearchService
        {
            Results = new[]
            {
                new AssetSearchResult("Code.h", file, false),
                new AssetSearchResult("Folder", directory, true)
            }
        };
        var builder = new ProjectAssetItemBuilder(registry, search, new TestFileExplorerProvider());

        var items = builder.BuildItemsForSearch("code", new ContextMenuViewModel(), TestActions());
        _items.AddRange(items);

        Assert.Equal(new[] { "Code.h", "Folder" }, items.Select(item => item.Name.Value));
        Assert.Equal(ProjectAssetType.Script, items[0].AssetType);
        Assert.Equal("code-id", items[0].AssetId);
        Assert.True(items[1].IsDirectory);
        Assert.Empty(items[1].AssetId);
        Assert.Equal(new[] { "code" }, search.Queries);
    }

    /// <summary>
    /// Missing directory, explorer, or search dependency returns empty list without querying services.
    /// </summary>
    [AvaloniaFact]
    public void TestMissingInputsReturnEmptyItems()
    {
        var search = new TestAssetSearchService();
        var missing = _temporaryDirectory.GetPath("Missing");

        Assert.Empty(new ProjectAssetItemBuilder(null, search, new TestFileExplorerProvider())
            .BuildItemsForDirectory(missing, new ContextMenuViewModel(), TestActions()));
        Assert.Empty(new ProjectAssetItemBuilder(null, search, null)
            .BuildItemsForSearch("query", new ContextMenuViewModel(), TestActions()));
        Assert.Empty(search.Queries);
    }

    /// <summary>
    /// Creates inert action bundle for builder-only tests.
    /// </summary>
    private static ProjectAssetItemActions TestActions()
    {
        return new ProjectAssetItemActions((_, _) => { }, _ => { }, _ => { }, _ => { }, (_, _) => { }, _ => { }, _ => { });
    }
}
