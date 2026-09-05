using System.Diagnostics.CodeAnalysis;
using Avalonia.Headless.XUnit;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies asset and component-reference editors synchronize nested IDs, missing state, activation, and lifetime.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "PropertyEditors")]
public sealed class ReferencePropertyViewModelTests
{
    /// <summary>
    /// Supplies empty asset searches because tests assign registered paths directly.
    /// </summary>
    private sealed class TestAssetSearchService : IAssetSearchService
    {
        public IReadOnlyList<AssetSearchResult> Search(string query) => [];

        public IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions) => [];
    }

    /// <summary>
    /// Maps reference template to one deterministic texture extension.
    /// </summary>
    private sealed class TestAssetTypeMapper : IAssetTypeMapper
    {
        public AssetType GetAssetTypeForTemplateType(string? templateTypeName) => AssetType.Texture;

        public IReadOnlyList<string> GetExtensionsForAssetType(AssetType assetType) => [".png"];
    }

    /// <summary>
    /// Records asset-focus activation requests.
    /// </summary>
    private sealed class TestProjectAssetFocusService : IProjectAssetFocusService
    {
        public event Action<string>? FocusAssetRequested;
        public event Action<string>? FocusAssetPathRequested;
        public List<string> AssetIds { get; } = [];

        public void FocusAsset(string assetId)
        {
            AssetIds.Add(assetId);
            FocusAssetRequested?.Invoke(assetId);
        }

        public void FocusAssetPath(string assetPath) => FocusAssetPathRequested?.Invoke(assetPath);
    }

    /// <summary>
    /// Supplies component-name lookup for scene entity filtering.
    /// </summary>
    private sealed class TestBehaviourRegistry : IBehaviourRegistry
    {
        public IReadOnlyDictionary<int, BehaviourAssetInfo> Behaviours => new Dictionary<int, BehaviourAssetInfo>();
        public int? RequiredId { get; set; }

        public bool TryGetById(int id, [NotNullWhen(true)] out BehaviourAssetInfo? behaviour)
        {
            behaviour = null;
            return false;
        }

        public int? GetIdByName(string name) => name == "Target" ? RequiredId : null;

        public int AllocateBehaviourId() => throw new NotSupportedException();

        public Task RefreshBehaviours() => throw new NotSupportedException();
    }

    /// <summary>
    /// Records component-reference selection without engine calls.
    /// </summary>
    private sealed class TestSelectionService : ISelectionService
    {
        public ReiEditor.Utils.Common.Observable<ISelectable?> Active { get; } = new(null);
        public ReiEditor.Utils.Common.Observable<IReadOnlyCollection<ISelectable>> Changed { get; } = new([]);
        public ReiEditor.Utils.Common.IObservable<ISelectable?> ActiveSelection => Active;
        public ReiEditor.Utils.Common.IObservable<IReadOnlyCollection<ISelectable>> SelectionChanged => Changed;
        public IReadOnlyCollection<ISelectable> SelectedItems => [];
        public GameEntity? SelectedEntity { get; private set; }

        public void Select(ISelectable selectable) { }
        public void Select(GameEntity e, bool sendToEngine = true) => SelectedEntity = e;
        public void AddSelection(GameEntity e, bool sendToEngine = true) => throw new NotSupportedException();
        public void Deselect(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void Deselect(GameEntity e, bool sendToEngine = true) => throw new NotSupportedException();
        public bool IsSelected(ISelectable selectable) => false;
        public bool IsEntitySelected(GameEntity e) => false;
        public IEntitySelectable? GetEntitySelectable(GameEntity e) => null;
        public void SetSelection(IReadOnlyCollection<ISelectable> selectables, ISelectable? primarySelection = null, bool sendToEngine = true) => throw new NotSupportedException();
        public void AddSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void RemoveSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void ToggleSelection(ISelectable selectable, bool sendToEngine = true) => throw new NotSupportedException();
        public void ResetSelection(bool sendToEngine = true) => throw new NotSupportedException();
        public void RegisterSelectable(ISelectable selectable) { }
        public void UnregisterSelectable(ISelectable selectable) { }
    }

    /// <summary>
    /// Asset editor reflects nested ID changes, commits picker assignments, and focuses activated asset.
    /// </summary>
    [AvaloniaFact]
    public void AssetReferenceSynchronizesCommitAndActivation()
    {
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets([new AssetInfo(new AssetMeta("first"), "C:/Assets/first.png"), new AssetInfo(new AssetMeta("second"), "C:/Assets/second.png")]);
        var property = TestAssetReference("first");
        var id = TestChildren(property)["Id"];
        var focus = new TestProjectAssetFocusService();
        using var viewModel = new AssetPropertyViewModel(property, new TestAssetSearchService(), registry, new TestAssetTypeMapper(), focus);

        Assert.Equal("first", viewModel.AssetPicker!.SelectedAssetId);
        Assert.True(viewModel.AssetPicker.TryAssignAssetFromPath("C:/Assets/second.png"));
        Assert.Equal("second", id.Value);

        viewModel.AssetPicker.ActivateAsset();
        Assert.Equal(new[] { "second" }, focus.AssetIds);

        id.Value = "missing";
        Assert.True(viewModel.AssetPicker.IsMissingAsset);
        Assert.Equal("missing", viewModel.AssetPicker.SelectedAssetId);
    }

    /// <summary>
    /// Disposed asset editor stops observing nested ID changes.
    /// </summary>
    [AvaloniaFact]
    public void AssetReferenceDisposeUnsubscribesNestedId()
    {
        var property = TestAssetReference("first");
        var id = TestChildren(property)["Id"];
        var registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        var viewModel = new AssetPropertyViewModel(property, new TestAssetSearchService(), registry, new TestAssetTypeMapper(), new TestProjectAssetFocusService());
        var picker = viewModel.AssetPicker!;

        viewModel.Dispose();
        id.Value = "later";

        Assert.Equal("first", picker.SelectedAssetId);
    }

    /// <summary>
    /// Component editor lists only matching entities, writes selection, reports missing references, and activates selection.
    /// </summary>
    [AvaloniaFact]
    public void ComponentReferenceFiltersCommitsReportsMissingAndActivates()
    {
        const int componentId = 42;
        var registry = new TestBehaviourRegistry { RequiredId = componentId };
        var scenes = new TestSceneManagementService();
        var selection = new TestSelectionService();
        var scene = new Scene("test");
        var matching = new GameEntity(2, "Matching");
        matching.AddBehaviour(new BehaviourComponent(componentId));
        scene.AddEntity(new GameEntity(1, "Other"));
        scene.AddEntity(matching);
        scenes.Scene.Value = scene;
        var property = TestComponentReference(0);
        var id = TestChildren(property)["SceneEntityId"];
        using var viewModel = new ComponentRefPropertyViewModel(property, new AssetRegistry(new TestLogger<AssetRegistry>()), registry, scenes, selection);

        viewModel.Picker!.RefreshSearchResultsForAll();
        var entry = Assert.Single(viewModel.Picker.SearchResults);
        Assert.Equal("Matching (2)", entry.Name);
        entry.SelectCommand.Execute(null);
        Assert.Equal(2, id.Value);

        viewModel.Picker.ActivateAsset();
        Assert.Same(matching, selection.SelectedEntity);

        id.Value = 99;
        Assert.True(viewModel.Picker.IsMissingAsset);
        Assert.Equal("missing entity (99)", viewModel.Picker.AssetName);
    }

    /// <summary>
    /// Scene rebuild replaces component picker, while disposal stops later rebuild reactions.
    /// </summary>
    [AvaloniaFact]
    public void ComponentReferenceRebuildAndDisposeRespectLifetime()
    {
        var scenes = new TestSceneManagementService();
        var scene = new Scene("test");
        scenes.Scene.Value = scene;
        var property = TestComponentReference(0);
        var viewModel = new ComponentRefPropertyViewModel(
            property,
            new AssetRegistry(new TestLogger<AssetRegistry>()),
            new TestBehaviourRegistry { RequiredId = 42 },
            scenes,
            new TestSelectionService());
        var initial = viewModel.Picker;

        scene.RebuildHierarchy();
        Assert.NotSame(initial, viewModel.Picker);
        var rebuilt = viewModel.Picker;

        viewModel.Dispose();
        scene.RebuildHierarchy();
        Assert.Same(rebuilt, viewModel.Picker);
    }

    private static SerializedProperty TestAssetReference(string id)
    {
        var property = new SerializedProperty("texture", SerializedTypeEnum.Custom, null, "AssetRef<Texture>", null, "Texture");
        property.Value = new Dictionary<string, SerializedProperty>
        {
            ["Id"] = new("Id", SerializedTypeEnum.String, id, "string", property)
        };
        return property;
    }

    private static SerializedProperty TestComponentReference(int sceneEntityId)
    {
        var property = new SerializedProperty("target", SerializedTypeEnum.Custom, null, "ComponentRef<Target>", null, "Target");
        property.Value = new Dictionary<string, SerializedProperty>
        {
            ["SceneEntityId"] = new("SceneEntityId", SerializedTypeEnum.Integer, sceneEntityId, "int", property)
        };
        return property;
    }

    private static Dictionary<string, SerializedProperty> TestChildren(SerializedProperty property)
        => Assert.IsType<Dictionary<string, SerializedProperty>>(property.Value);
}
