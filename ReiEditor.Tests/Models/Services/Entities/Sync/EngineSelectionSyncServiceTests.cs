using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Entities.Sync;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Entities.Sync;

/// <summary>
/// Verifies engine selection snapshots apply safely on UI thread without echoing changes to engine.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "EntitySync")]
public sealed class EngineSelectionSyncServiceTests
{
    private sealed class TestEntitySelectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class TestAssetSelectable : IAssetSelectable
    {
        public string AssetId => "asset-id";
        public string AssetName => "Asset";
        public string AssetPath => "Assets/Asset.mat";
        public bool IsAssetSupportedInMonitor => true;
        public void Select() { }
        public void Deselect() { }
    }

    /// <summary>
    /// Known engine-selected IDs replace editor entity selection without sending selection back to engine.
    /// </summary>
    [AvaloniaFact]
    public void TestKnownSnapshotUpdatesEditorSelectionWithoutEngineSendBack()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var entities = new[]
        {
            new GameEntity(1, "First"),
            new GameEntity(2, "Second"),
            new GameEntity(3, "Third"),
        };
        var selectables = entities.Select(entity => new TestEntitySelectable(entity)).ToArray();
        foreach (var selectable in selectables) selection.RegisterSelectable(selectable);
        selection.SetSelection(new[] { selectables[0] }, selectables[0], sendToEngine: false);
        var service = new EngineSelectionSyncService(selection);

        service.ApplySelection(entities, new HashSet<int> { 2, 3 });
        Assert.True(new HashSet<int> { 2, 3 }.SetEquals(selection.SelectedItems
            .OfType<IEntitySelectable>()
            .Select(selectable => selectable.Entity.Id)));
        Assert.Same(selectables[1], selection.ActiveSelection.Value);
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Snapshot retaining current primary entity preserves that primary while adding other selected entities.
    /// </summary>
    [AvaloniaFact]
    public void TestSnapshotPreservesCurrentPrimaryWhenStillSelected()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var first = new GameEntity(1, "First");
        var second = new GameEntity(2, "Second");
        var firstSelectable = new TestEntitySelectable(first);
        var secondSelectable = new TestEntitySelectable(second);
        selection.RegisterSelectable(firstSelectable);
        selection.RegisterSelectable(secondSelectable);
        selection.SetSelection(new[] { secondSelectable }, secondSelectable, sendToEngine: false);
        var service = new EngineSelectionSyncService(selection);

        service.ApplySelection(new[] { first, second }, new HashSet<int> { 1, 2 });
        Assert.Same(secondSelectable, selection.ActiveSelection.Value);
        Assert.Equal(2, selection.SelectedItems.Count);
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Empty engine entity selection leaves active non-entity selection unchanged.
    /// </summary>
    [AvaloniaFact]
    public void TestEmptySnapshotDoesNotResetNonEntitySelection()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var asset = new TestAssetSelectable();
        selection.Select(asset);
        var service = new EngineSelectionSyncService(selection);

        service.ApplySelection(Array.Empty<GameEntity>(), new HashSet<int>());
        Assert.Same(asset, Assert.Single(selection.SelectedItems));
        Assert.Same(asset, selection.ActiveSelection.Value);
        Assert.Empty(api.Selections);
    }

}
