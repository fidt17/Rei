using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.Tests.ViewModels.Controls;

/// <summary>Verifies picker selection, missing references, search filtering and callback lifetime.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "AssetPicker")]
public sealed class AssetPickerViewModelTests
{
    private sealed class TestSearchService : IAssetSearchService
    {
        public IReadOnlyList<AssetSearchResult> Results { get; set; } = [];
        public List<(string Query, string[] Extensions)> Requests { get; } = [];
        public IReadOnlyList<AssetSearchResult> Search(string query) => throw new NotSupportedException();
        public IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions)
        {
            Requests.Add((query, extensions.ToArray()));
            return Results;
        }
    }

    /// <summary>Sync trims IDs without committing, while assignment and clear publish updated selection before events.</summary>
    [Fact]
    public void SyncCommitClearAndActivateHaveDistinctEffects()
    {
        var changes = new List<(string? Id, string? Path)>();
        using var vm = new AssetPickerViewModel(Registry(), Entries(), (id, path) => changes.Add((id, path)));
        var selected = new List<string>();
        var activated = 0;
        vm.AssetSelectedEvent += () => selected.Add(vm.SelectedAssetId);
        vm.AssetActivatedEvent += () => activated++;
        vm.SyncSelectedAsset(" a ");
        Assert.Equal("Alpha", vm.AssetName);
        Assert.True(vm.HasActiveAsset);
        Assert.Empty(changes);
        Assert.Empty(selected);
        vm.ActivateAsset();
        Assert.True(vm.TryAssignAssetFromPath("C:/ASSETS/B.MAT"));
        Assert.Equal("beta", vm.AssetName);
        vm.ClearAsset();
        vm.ActivateAsset();
        Assert.Equal(new (string?, string?)[] { ("b", "C:/Assets/b.mat"), ("", null) }, changes);
        Assert.Equal(new[] { "b", "" }, selected);
        Assert.Equal(1, activated);
        Assert.False(vm.HasActiveAsset);
        Assert.False(vm.IsMissingAsset);
    }

    /// <summary>Blank IDs use empty state, absent IDs use missing state and custom resolvers control missing labels.</summary>
    [Theory]
    [InlineData(null, "empty", false)]
    [InlineData("  ", "empty", false)]
    [InlineData("lost", "missing asset", true)]
    public void MissingAndEmptyStatesAreDistinct(string? id, string label, bool missing)
    {
        using var vm = new AssetPickerViewModel(Registry(), Entries(), null);
        vm.SyncSelectedAsset(id);
        Assert.Equal(label, vm.AssetName);
        Assert.Equal(missing, vm.IsMissingAsset);
        Assert.False(vm.HasActiveAsset);
        using var custom = new AssetPickerViewModel(Registry(), Entries(), null, missingEntryStateFactory: value => ("unresolved:" + value, false));
        custom.SyncSelectedAsset("custom");
        Assert.Equal("unresolved:custom", custom.AssetName);
        Assert.False(custom.IsMissingAsset);
    }

    /// <summary>Entry browsing sorts all names, query matching ignores case, reset preserves results and clear refreshes all.</summary>
    [Fact]
    public void EntrySearchResetAndDisposeRespectRefreshBoundaries()
    {
        using var vm = new AssetPickerViewModel(Registry(), Entries(), null);
        vm.RefreshSearchResultsForAll();
        Assert.Equal(new[] { "Alpha", "beta" }, vm.SearchResults.Select(x => x.Name));
        vm.SearchField.Query.Value = "BETA";
        Assert.Equal("b", Assert.Single(vm.SearchResults).AssetId);
        vm.SearchField.ResetSearch();
        Assert.Single(vm.SearchResults);
        vm.SearchField.Query.Value = "none";
        Assert.Empty(vm.SearchResults);
        vm.SearchField.ClearCommand.Execute(null);
        Assert.Equal(2, vm.SearchResults.Count);
        vm.Dispose();
        vm.SearchField.Query.Value = "none";
        Assert.Equal(2, vm.SearchResults.Count);
    }

    /// <summary>Registry mode rejects unsupported/unregistered paths and excludes directories or stale search results.</summary>
    [Fact]
    public void RegistrySearchAndAssignmentValidateAssetMetadata()
    {
        var registry = Registry();
        registry.RegisterNewAssets([new AssetInfo(new AssetMeta("mat"), "C:/Assets/surface.MAT"), new AssetInfo(new AssetMeta("scene"), "C:/Assets/scene.scene")]);
        var search = new TestSearchService { Results = [new("directory", "C:/Assets", true), new("lost", "C:/Assets/lost.mat", false), new("surface", "C:/Assets/surface.MAT", false)] };
        var changes = new List<string?>();
        using var vm = new AssetPickerViewModel(search, registry, [".mat"], (id, _) => changes.Add(id));
        Assert.False(vm.CanAcceptAssetPath(" "));
        Assert.False(vm.TryAssignAssetFromPath("C:/Assets/scene.scene"));
        Assert.False(vm.TryAssignAssetFromPath("C:/Assets/lost.mat"));
        Assert.Empty(changes);
        vm.SyncSelectedAsset("scene");
        Assert.True(vm.IsMissingAsset);
        vm.SearchField.Query.Value = "surface";
        var item = Assert.Single(vm.SearchResults);
        Assert.Equal("surface", item.Name);
        Assert.Equal("mat", item.AssetId);
        Assert.Equal("surface", Assert.Single(search.Requests).Query);
        Assert.Equal(new[] { ".mat" }, search.Requests[0].Extensions);
        item.SelectCommand.Execute(null);
        Assert.Equal(new[] { "mat" }, changes);
        Assert.True(vm.HasActiveAsset);
        Assert.True(vm.TryAssignAssetFromPath("C:/Assets/surface.MAT"));
    }

    /// <summary>Empty entry or extension lists disable selection and produce no search candidates.</summary>
    [Fact]
    public void UnsupportedSelectionDoesNotSearchOrAssign()
    {
        var search = new TestSearchService();
        using var files = new AssetPickerViewModel(search, Registry(), [], null);
        using var entries = new AssetPickerViewModel(Registry(), Array.Empty<AssetPickerViewModel.Entry>(), null);
        foreach (var vm in new[] { files, entries })
        {
            Assert.False(vm.IsSelectionSupported);
            Assert.False(vm.TryAssignAssetFromPath("C:/Assets/a.mat"));
            vm.SearchField.Query.Value = "a";
            vm.RefreshSearchResultsForAll();
            Assert.Empty(vm.SearchResults);
        }
        Assert.Empty(search.Requests);
    }

    private static AssetRegistry Registry() => new(new TestLogger<AssetRegistry>());
    private static AssetPickerViewModel.Entry[] Entries() => [new("beta", "C:/Assets/b.mat", "b"), new("Alpha", "C:/Assets/a.mat", "a")];
}
