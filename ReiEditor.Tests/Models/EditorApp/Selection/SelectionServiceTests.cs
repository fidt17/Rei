using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.EditorApp.Selection;

/// <summary>
/// Verifies editor selection state and selection requests sent to the engine boundary.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Selection")]
public sealed class SelectionServiceTests
{
    private sealed class EntitySelectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;

        public void Select() { }
        public void Deselect() { }
    }

    /// <summary>
    /// Selecting the same registered entity twice publishes one state change and sends its ID to the engine once.
    /// </summary>
    [Fact]
    public void SelectingRegisteredEntityPublishesSelectionAndSendsItsIdOnce()
    {
        var api = new TestEntityApi();
        var service = new SelectionService(api);
        var entity = new GameEntity(42, "Camera");
        var selectable = new EntitySelectable(entity);
        service.RegisterSelectable(selectable);
        var notifications = new List<IReadOnlyCollection<ISelectable>>();
        void RecordSelection(IReadOnlyCollection<ISelectable> selection) => notifications.Add(selection);
        service.SelectionChanged.Subscribe(RecordSelection, invoke: false);

        try
        {
            service.Select(entity);
            service.Select(entity);

            Assert.Same(selectable, service.ActiveSelection.Value);
            Assert.Same(selectable, Assert.Single(service.SelectedItems));
            Assert.Same(selectable, Assert.Single(Assert.Single(notifications)));
            Assert.Equal(new[] { 42 }, Assert.Single(api.Selections));
        }
        finally
        {
            service.SelectionChanged.Unsubscribe(RecordSelection);
            service.UnregisterSelectable(selectable);
        }
    }
}
