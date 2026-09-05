using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Entities;

/// <summary>
/// Verifies selected entity action target resolution, command dispatch, and result application.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Entities")]
public sealed class SelectedEntityActionServiceTests
{
    private sealed class TestEntitySelectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    private sealed class TestDeleteCommand : ISelectedEntityDeleteCommand
    {
        public List<SelectedEntityCommandTarget> Targets { get; } = new();
        public Action<SelectedEntityCommandTarget>? OnExecute { get; set; }

        public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
        {
            Targets.Add(target);
            OnExecute?.Invoke(target);
            return new SelectedEntityCommandResult();
        }
    }

    private sealed class TestDuplicateCommand : ISelectedEntityDuplicateCommand
    {
        public List<SelectedEntityCommandTarget> Targets { get; } = new();
        public SelectedEntityCommandResult Result { get; set; } = new();
        public Action<SelectedEntityCommandTarget>? OnExecute { get; set; }

        public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
        {
            Targets.Add(target);
            OnExecute?.Invoke(target);
            return Result;
        }
    }

    private sealed class TestRenameCommand : ISelectedEntityRenameCommand
    {
        public List<SelectedEntityCommandTarget> Targets { get; } = new();
        public SelectedEntityCommandResult Result { get; set; } = new();

        public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
        {
            Targets.Add(target);
            return Result;
        }
    }

    /// <summary>
    /// Delete action collapses selected descendants and sorts remaining targets by order then ID.
    /// </summary>
    [Fact]
    public void TestDeleteCollapsesNestedSelectionAndOrdersTargets()
    {
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var sceneService = new TestSceneManagementService();
        var scene = new Scene("Scene");
        var parent = new GameEntity(10, "Parent");
        var child = new GameEntity(11, "Child");
        var sameOrderLowerId = new GameEntity(2, "Lower");
        var sameOrderHigherId = new GameEntity(3, "Higher");
        foreach (var entity in new[] { parent, child, sameOrderLowerId, sameOrderHigherId }) scene.AddEntity(entity);
        Assert.True(scene.MoveEntity(child, parent, 0));
        parent.Transform.SetOrder(5);
        sameOrderLowerId.Transform.SetOrder(1);
        sameOrderHigherId.Transform.SetOrder(1);
        sceneService.Scene.Value = scene;
        var stale = new GameEntity(99, "Stale");
        var selectables = new[] { parent, child, sameOrderHigherId, sameOrderLowerId, parent, stale }
            .Select(entity => new TestEntitySelectable(entity))
            .ToArray();
        foreach (var selectable in selectables) selection.RegisterSelectable(selectable);
        selection.SetSelection(selectables, selectables[0], sendToEngine: false);
        var delete = new TestDeleteCommand();
        var selectionWasResetBeforeCommand = false;
        delete.OnExecute = _ => selectionWasResetBeforeCommand = selection.SelectedItems.Count == 0;
        var service = CreateService(selection, sceneService, api, delete: delete);

        Assert.True(service.DeleteSelectedEntity());

        var target = Assert.Single(delete.Targets);
        Assert.Same(parent, target.PrimaryEntity);
        Assert.Equal(new[] { sameOrderLowerId, sameOrderHigherId, parent }, target.Entities);
        Assert.True(selectionWasResetBeforeCommand);
        Assert.Empty(selection.SelectedItems);
    }

    /// <summary>
    /// Action rejects primary entity absent from current scene and does not dispatch command.
    /// </summary>
    [Fact]
    public void TestActionRejectsStalePrimaryEntity()
    {
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var stale = new TestEntitySelectable(new GameEntity(99, "Stale"));
        selection.SetSelection(new[] { stale }, stale, sendToEngine: false);
        var sceneService = new TestSceneManagementService();
        sceneService.Scene.Value = new Scene("Current");
        var delete = new TestDeleteCommand();
        var service = CreateService(selection, sceneService, api, delete: delete);

        Assert.False(service.DeleteSelectedEntity());
        Assert.Empty(delete.Targets);
        Assert.Same(stale, Assert.Single(selection.SelectedItems));
    }

    /// <summary>
    /// Duplicate result sends new IDs and selects registered result entities with first ID as primary.
    /// </summary>
    [Fact]
    public void TestDuplicateAppliesResultSelectionAndPrimary()
    {
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var sceneService = new TestSceneManagementService();
        var scene = new Scene("Scene");
        var source = new GameEntity(1, "Source");
        var duplicateA = new GameEntity(20, "Duplicate A");
        var duplicateB = new GameEntity(21, "Duplicate B");
        foreach (var entity in new[] { source, duplicateA, duplicateB }) scene.AddEntity(entity);
        sceneService.Scene.Value = scene;
        var sourceSelectable = new TestEntitySelectable(source);
        var duplicateASelectable = new TestEntitySelectable(duplicateA);
        var duplicateBSelectable = new TestEntitySelectable(duplicateB);
        foreach (var selectable in new[] { sourceSelectable, duplicateASelectable, duplicateBSelectable })
        {
            selection.RegisterSelectable(selectable);
        }
        selection.SetSelection(new[] { sourceSelectable }, sourceSelectable, sendToEngine: false);
        var duplicate = new TestDuplicateCommand
        {
            Result = new SelectedEntityCommandResult(new[] { 21, 20 }),
        };
        var service = CreateService(selection, sceneService, api, duplicate: duplicate);

        Assert.True(service.DuplicateSelectedEntity());

        Assert.Equal(new[] { 21, 20 }, Assert.Single(api.Selections));
        Assert.Equal(2, selection.SelectedItems.Count);
        Assert.Contains(duplicateASelectable, selection.SelectedItems);
        Assert.Contains(duplicateBSelectable, selection.SelectedItems);
        Assert.Same(duplicateBSelectable, selection.ActiveSelection.Value);
    }

    /// <summary>
    /// Rename action publishes command result ID to rename request subscribers.
    /// </summary>
    [Fact]
    public void TestRenamePublishesRequestedEntityId()
    {
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var selectable = new TestEntitySelectable(new GameEntity(7, "Rename"));
        selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        var rename = new TestRenameCommand { Result = new SelectedEntityCommandResult(RenameEntityId: 7) };
        var service = CreateService(selection, new TestSceneManagementService(), api, rename: rename);
        var requestedIds = new List<int>();
        service.RenameEntityRequested += requestedIds.Add;

        Assert.True(service.RequestRenameSelectedEntity());

        Assert.Equal(new[] { 7 }, requestedIds);
        Assert.Single(rename.Targets);
    }

    /// <summary>
    /// Reentrant action is rejected while command runs and guard resets after command exception.
    /// </summary>
    [Fact]
    public void TestActionGuardRejectsReentryAndResetsAfterException()
    {
        var api = new TestEntityApi();
        var selection = new SelectionService(api);
        var selectable = new TestEntitySelectable(new GameEntity(4, "Selected"));
        selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        var duplicate = new TestDuplicateCommand();
        SelectedEntityActionService? service = null;
        var reentryResults = new List<bool>();
        duplicate.OnExecute = _ =>
        {
            reentryResults.Add(service!.DuplicateSelectedEntity());
            throw new InvalidOperationException("failed");
        };
        service = CreateService(selection, new TestSceneManagementService(), api, duplicate: duplicate);

        Assert.Throws<InvalidOperationException>(() => service.DuplicateSelectedEntity());
        Assert.Throws<InvalidOperationException>(() => service.DuplicateSelectedEntity());

        Assert.Equal(new[] { false, false }, reentryResults);
        Assert.Equal(2, duplicate.Targets.Count);
    }

    private static SelectedEntityActionService CreateService(
        ISelectionService selection,
        ISceneManagementService sceneService,
        IEntityApi api,
        ISelectedEntityDeleteCommand? delete = null,
        ISelectedEntityDuplicateCommand? duplicate = null,
        ISelectedEntityRenameCommand? rename = null)
    {
        return new SelectedEntityActionService(
            selection,
            sceneService,
            api,
            delete ?? new TestDeleteCommand(),
            duplicate ?? new TestDuplicateCommand(),
            rename ?? new TestRenameCommand());
    }
}
