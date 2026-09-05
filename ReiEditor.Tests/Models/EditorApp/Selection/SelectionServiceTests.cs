using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.Selection;

/// <summary>
/// Verifies editor selection state and engine selection synchronization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Selection")]
public sealed class SelectionServiceTests
{
    private sealed class TestEntitySelectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class TestAssetSelectable(string id) : IAssetSelectable
    {
        public string AssetId { get; } = id;
        public string AssetName => $"Asset {AssetId}";
        public string AssetPath => $"Assets/{AssetId}.png";
        public bool IsAssetSupportedInMonitor => true;
        public void Select() { }
        public void Deselect() { }
    }

    /// <summary>
    /// Selecting same registered entity twice publishes one state change and sends its ID once.
    /// </summary>
    [Fact]
    public void TestSelectingRegisteredEntityPublishesSelectionAndSendsItsIdOnce()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var entity = new GameEntity(42, "Camera");
        var selectable = new TestEntitySelectable(entity);
        service.RegisterSelectable(selectable);
        var notifications = new List<IReadOnlyCollection<ISelectable>>();
        service.SelectionChanged.Subscribe(notifications.Add, invoke: false);

        service.Select(entity);
        service.Select(entity);

        Assert.Same(selectable, service.ActiveSelection.Value);
        Assert.Same(selectable, Assert.Single(service.SelectedItems));
        Assert.Same(selectable, Assert.Single(Assert.Single(notifications)));
        Assert.Equal(new[] { 42 }, Assert.Single(api.Selections));
    }

    /// <summary>
    /// SetSelection removes duplicate references and honors a primary member or falls back to first item.
    /// </summary>
    [Fact]
    public void TestSetSelectionDeduplicatesAndResolvesPrimarySelection()
    {
        var service = new SelectionService(new TestEntityApi());
        var first = new TestAssetSelectable("first");
        var second = new TestAssetSelectable("second");

        service.SetSelection(new ISelectable[] { first, second, first }, second);
        Assert.Equal(2, service.SelectedItems.Count);
        Assert.Same(second, service.ActiveSelection.Value);

        service.SetSelection(new ISelectable[] { first, second }, new TestAssetSelectable("outside"));
        Assert.Same(first, service.ActiveSelection.Value);
    }

    /// <summary>
    /// Add, toggle, remove, and reset update membership and primary selection through each transition.
    /// </summary>
    [Fact]
    public void TestSelectionMutationOperationsUpdateMembershipAndPrimary()
    {
        var service = new SelectionService(new TestEntityApi());
        var first = new TestAssetSelectable("first");
        var second = new TestAssetSelectable("second");

        service.AddSelection(first);
        service.AddSelection(second);
        Assert.Same(second, service.ActiveSelection.Value);
        Assert.Equal(2, service.SelectedItems.Count);

        service.ToggleSelection(second);
        Assert.Same(first, service.ActiveSelection.Value);
        Assert.Same(first, Assert.Single(service.SelectedItems));

        service.RemoveSelection(first);
        Assert.Null(service.ActiveSelection.Value);
        Assert.Empty(service.SelectedItems);

        service.AddSelection(first);
        service.ResetSelection();
        Assert.Null(service.ActiveSelection.Value);
        Assert.Empty(service.SelectedItems);
    }

    /// <summary>
    /// Unregistered entities cannot be selected through entity overloads.
    /// </summary>
    [Fact]
    public void TestUnregisteredEntitySelectionIsIgnored()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var entity = new GameEntity(7, "Missing");

        service.Select(entity);
        service.AddSelection(entity);
        service.Deselect(entity);

        Assert.Empty(service.SelectedItems);
        Assert.Null(service.ActiveSelection.Value);
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Combining entity and asset selections clears native entity selection and avoids duplicate native requests.
    /// </summary>
    [Fact]
    public void TestMixedSelectionClearsNativeEntitySelectionOnce()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var entity = new GameEntity(8, "Entity");
        var entitySelectable = new TestEntitySelectable(entity);
        var assetSelectable = new TestAssetSelectable("texture");
        service.RegisterSelectable(entitySelectable);

        service.Select(entity);
        service.AddSelection(assetSelectable);
        service.SetSelection(new ISelectable[] { assetSelectable, entitySelectable }, assetSelectable);

        Assert.Equal(2, api.Selections.Count);
        Assert.Equal(new[] { 8 }, api.Selections[0]);
        Assert.Empty(api.Selections[1]);
    }

    /// <summary>
    /// Distinct selectables for same entity ID produce one native ID and primary-only changes do not resend it.
    /// </summary>
    [Fact]
    public void TestEntitySelectionDeduplicatesNativeIdsAndIgnoresPrimaryOnlyChanges()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var first = new TestEntitySelectable(new GameEntity(9, "First instance"));
        var second = new TestEntitySelectable(new GameEntity(9, "Second instance"));

        service.SetSelection(new ISelectable[] { first, second }, first);
        service.SetSelection(new ISelectable[] { first, second }, second);

        Assert.Equal(new[] { 9 }, Assert.Single(api.Selections));
        Assert.Same(second, service.ActiveSelection.Value);
        Assert.Equal(2, service.SelectedItems.Count);
    }

    /// <summary>
    /// Unregistering selected primary chooses remaining item and publishes snapshot without engine request.
    /// </summary>
    [Fact]
    public void TestUnregisterSelectedPrimaryUpdatesSelectionWithoutEngineRequest()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var first = new TestEntitySelectable(new GameEntity(1, "First"));
        var second = new TestEntitySelectable(new GameEntity(2, "Second"));
        service.RegisterSelectable(first);
        service.RegisterSelectable(second);
        service.SetSelection(new ISelectable[] { first, second }, first, sendToEngine: false);
        var snapshots = new List<IReadOnlyCollection<ISelectable>>();
        service.SelectionChanged.Subscribe(snapshots.Add, invoke: false);

        service.UnregisterSelectable(first);

        Assert.Same(second, service.ActiveSelection.Value);
        Assert.Same(second, Assert.Single(service.SelectedItems));
        Assert.Same(second, Assert.Single(Assert.Single(snapshots)));
        Assert.Empty(api.Selections);
    }

    /// <summary>
    /// Silent synchronization updates cached IDs so a later identical selection does not resend them.
    /// </summary>
    [Fact]
    public void TestSelectionWithoutEngineSendUpdatesSynchronizationCache()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var selectable = new TestEntitySelectable(new GameEntity(12, "Cached"));

        service.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        service.SetSelection(new[] { selectable }, selectable, sendToEngine: true);

        Assert.Empty(api.Selections);
    }
}
