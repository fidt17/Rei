using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.TransformationControls;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.TransformationControls;

/// <summary>Verifies selection-driven transformation capabilities and engine command routing.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Viewport")]
public sealed class TransformationControlsServiceTests
{
    /// <summary>Records transformation commands without loading native engine code.</summary>
    private sealed class TestApi : TestEngineApi
    {
        public List<(TransformationMode Mode, bool WorldSpace)> Changes { get; } = new();

        public override void ChangeTransformationMode(TransformationMode mode, bool worldSpace)
            => Changes.Add((mode, worldSpace));
    }

    /// <summary>Exposes entity identity to selection service.</summary>
    private sealed class TestSelectable(GameEntity entity) : IEntitySelectable
    {
        public GameEntity Entity { get; } = entity;
        public void Select() { }
        public void Deselect() { }
    }

    /// <summary>Zero or one ordinary entity allows both transformation spaces.</summary>
    [Fact]
    public void TestSingleEntityAllowsLocalAndWorldSpace()
    {
        var context = CreateContext();
        using var service = context.Service;
        var selectable = new TestSelectable(new GameEntity(1, "Entity"));

        context.Selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        service.SetLocalSpace();
        service.SetWorldSpace();

        Assert.True(service.CanUseLocalSpace);
        Assert.True(service.CanUseWorldSpace);
        Assert.Equal(
            new[]
            {
                (TransformationMode.Movement, false),
                (TransformationMode.Movement, true),
            },
            context.Api.Changes);
    }

    /// <summary>Multiple distinct ordinary entities force world space and reject a local-space request.</summary>
    [Fact]
    public void TestMultipleEntitiesForceWorldSpaceAndRejectLocalSpace()
    {
        var context = CreateContext();
        using var service = context.Service;
        service.SetLocalSpace();
        context.Api.Changes.Clear();
        var first = new TestSelectable(new GameEntity(1, "First"));
        var second = new TestSelectable(new GameEntity(2, "Second"));

        context.Selection.SetSelection(new[] { first, second }, first, sendToEngine: false);
        service.SetLocalSpace();

        Assert.False(service.CanUseLocalSpace);
        Assert.True(service.IsWorldSpace);
        Assert.Equal(new[] { (TransformationMode.Movement, true) }, context.Api.Changes);
    }

    /// <summary>Duplicate selectables for one entity ID count as one selected entity.</summary>
    [Fact]
    public void TestDuplicateEntityIdsDoNotDisableLocalSpace()
    {
        var context = CreateContext();
        using var service = context.Service;
        var first = new TestSelectable(new GameEntity(7, "First"));
        var duplicate = new TestSelectable(new GameEntity(7, "Duplicate"));

        context.Selection.SetSelection(new[] { first, duplicate }, first, sendToEngine: false);

        Assert.True(service.CanUseLocalSpace);
        Assert.True(service.CanUseWorldSpace);
    }

    /// <summary>RectTransform selection enables rect mode, forces local space, and blocks world-space requests.</summary>
    [Fact]
    public void TestRectTransformSelectionForcesLocalRectMode()
    {
        const int RECT_TRANSFORM_ID = 42;
        var context = CreateContext(RECT_TRANSFORM_ID);
        using var service = context.Service;
        var entity = new GameEntity(1, "Rect");
        entity.AddBehaviour(new BehaviourComponent(RECT_TRANSFORM_ID));
        var selectable = new TestSelectable(entity);

        context.Selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        service.SetMode(TransformationMode.RectTransform);
        var callCount = context.Api.Changes.Count;
        service.SetWorldSpace();

        Assert.True(service.CanUseRectTransformMode);
        Assert.False(service.CanUseWorldSpace);
        Assert.True(service.IsLocalSpace);
        Assert.Equal(TransformationMode.RectTransform, service.Mode);
        Assert.Equal(callCount, context.Api.Changes.Count);
        Assert.Equal((TransformationMode.RectTransform, false), context.Api.Changes[^1]);
    }

    /// <summary>Removing last RectTransform selection returns rect mode to movement.</summary>
    [Fact]
    public void TestRemovingRectTransformSelectionReturnsToMovement()
    {
        const int RECT_TRANSFORM_ID = 42;
        var context = CreateContext(RECT_TRANSFORM_ID);
        using var service = context.Service;
        var entity = new GameEntity(1, "Rect");
        entity.AddBehaviour(new BehaviourComponent(RECT_TRANSFORM_ID));
        var selectable = new TestSelectable(entity);
        context.Selection.SetSelection(new[] { selectable }, selectable, sendToEngine: false);
        service.SetMode(TransformationMode.RectTransform);

        context.Selection.ResetSelection(sendToEngine: false);

        Assert.False(service.CanUseRectTransformMode);
        Assert.Equal(TransformationMode.Movement, service.Mode);
        Assert.Equal((TransformationMode.Movement, false), context.Api.Changes[^1]);
    }

    /// <summary>Disposal detaches selection and engine-state subscriptions.</summary>
    [Fact]
    public void TestDisposeStopsSelectionAndEngineStateUpdates()
    {
        var context = CreateContext();
        context.Service.Dispose();
        var first = new TestSelectable(new GameEntity(1, "First"));
        var second = new TestSelectable(new GameEntity(2, "Second"));

        context.Selection.SetSelection(new[] { first, second }, first, sendToEngine: false);
        context.Runner.Active.Value = true;

        Assert.True(context.Service.CanUseLocalSpace);
        Assert.False(context.Service.EngineRunning);
        Assert.Empty(context.Api.Changes);
    }

    private static (TransformationControlsService Service, SelectionService Selection, TestApi Api, TestEngineRunner Runner) CreateContext(int? rectTransformId = null)
    {
        var api = new TestApi();
        var runner = new TestEngineRunner();
        var selection = new SelectionService(new TestEntityApi());
        var registry = new TestBehaviourRegistry();
        if (rectTransformId.HasValue) registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = rectTransformId.Value;
        return (new TransformationControlsService(api, runner, selection, registry), selection, api, runner);
    }
}
