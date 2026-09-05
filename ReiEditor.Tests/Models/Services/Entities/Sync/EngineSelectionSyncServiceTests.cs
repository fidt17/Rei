using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api.DTO;
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

        var applied = service.TryApplySelectionSnapshot(
            entities,
            Snapshot((2, true), (3, true)),
            new HashSet<int> { 1 });

        Assert.True(applied);
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

        var applied = service.TryApplySelectionSnapshot(
            new[] { first, second },
            Snapshot((1, true), (2, true)),
            new HashSet<int> { 2 });

        Assert.True(applied);
        Assert.Same(secondSelectable, selection.ActiveSelection.Value);
        Assert.Equal(2, selection.SelectedItems.Count);
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Snapshot referencing entity absent from current scene is rejected before editor selection changes.
    /// </summary>
    [AvaloniaFact]
    public void TestUnknownEngineSelectedIdRejectsSnapshot()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var entity = new GameEntity(1, "Known");
        var selectable = new TestEntitySelectable(entity);
        selection.RegisterSelectable(selectable);
        selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        var service = new EngineSelectionSyncService(selection);

        var applied = service.TryApplySelectionSnapshot(
            new[] { entity },
            Snapshot((99, true)),
            new HashSet<int> { 1 });

        Assert.False(applied);
        Assert.Same(selectable, Assert.Single(selection.SelectedItems));
        Assert.Same(selectable, selection.ActiveSelection.Value);
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

        var applied = service.TryApplySelectionSnapshot(
            Array.Empty<GameEntity>(),
            Snapshot(),
            new HashSet<int>());

        Assert.True(applied);
        Assert.Same(asset, Assert.Single(selection.SelectedItems));
        Assert.Same(asset, selection.ActiveSelection.Value);
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Snapshot captured before a newer editor selection does not overwrite newer selection state.
    /// </summary>
    [AvaloniaFact]
    public void TestStaleSnapshotGuardPreservesNewerEditorSelection()
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

        var accepted = service.TryApplySelectionSnapshot(
            new[] { first, second },
            Snapshot((1, true)),
            new HashSet<int> { 1 });

        Assert.True(accepted);
        Assert.Same(secondSelectable, Assert.Single(selection.SelectedItems));
        Assert.Same(secondSelectable, selection.ActiveSelection.Value);
        Assert.Empty(api.Selections);
    }

    private static GetSceneEntitiesResponse Snapshot(params (int Id, bool Selected)[] entities)
    {
        return new GetSceneEntitiesResponse
        {
            Entities = entities
                .Select(entity => new GetSceneEntitiesResponse.SceneEntitiesResponseEntity
                {
                    Id = entity.Id,
                    IsSelected = entity.Selected,
                })
                .ToList(),
        };
    }
}
