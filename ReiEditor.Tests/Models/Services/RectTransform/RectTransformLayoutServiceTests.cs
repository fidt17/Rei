using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Windows.Playmode;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.Services.RectTransform;

/// <summary>Verifies scene layout, canvas scaling and property updates without creating an engine window.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "RectTransform")]
public sealed class RectTransformLayoutServiceTests
{
    private sealed class TestWindowController : IEngineWindowController
    {
        public Observable<(int Width, int Height)?> Size { get; } = new(null);
        public ReiEditor.Utils.Common.IObservable<(int Width, int Height)?> ViewportSize => Size;
        public ReiEditor.Utils.Common.IObservable<IntPtr?> WindowPointer => throw new NotSupportedException();
        public void SetupWindow() => throw new NotSupportedException();
        public void DestroyWindow() => throw new NotSupportedException();
        public void SetViewportSize(int width, int height) => throw new NotSupportedException();
    }

    private readonly TestBehaviourRegistry _registry = new();
    private readonly TestSceneManagementService _scenes = new();
    private readonly TestWindowController _window = new();
    private const int RECT_ID = 10;
    private const int CANVAS_ID = 20;

    public RectTransformLayoutServiceTests()
    {
        _registry.Ids[EngineBehavioursConstants.RECT_TRANSFORM] = RECT_ID;
        _registry.Ids[EngineBehavioursConstants.CANVAS] = CANVAS_ID;
    }

    /// <summary>Entities without canvas or parent data use the documented fallback size and reject missing layout.</summary>
    [Fact]
    public void MissingComponentsUseFallbackAndRejectPreservation()
    {
        var service = CreateService();
        var entity = new GameEntity(1, "plain");
        Assert.Equal(new RectTransformVector2(1920, 1080), service.GetParentSize(entity));
        Assert.False(service.TryGetRectTransform(entity, out _));
        Assert.False(service.TryPreserveRectForParent(entity, null, out _));
        Assert.False(service.TryReadLayout(new BehaviourComponent(RECT_ID), out _));
        Assert.False(service.TryPreserveRectForPivot(entity, new BehaviourComponent(RECT_ID), 0, 0, out _));
    }

    /// <summary>Canvas scale modes use viewport dimensions or the selected reference axis, clamping the match factor.</summary>
    [Theory]
    [InlineData(0, 0f, 1600f, 900f)]
    [InlineData(1, 0f, 800f, 450f)]
    [InlineData(1, 1f, 1600f / 3, 300f)]
    [InlineData(1, 0.5f, 640f, 360f)]
    [InlineData(1, -4f, 800f, 450f)]
    [InlineData(1, 4f, 1600f / 3, 300f)]
    public void CanvasScaleRespectsModeAndMatch(int mode, float match, float width, float height)
    {
        var entity = CreateCanvas(800, 300, mode, match);
        _window.Size.Value = (1600, 900);
        var size = CreateService().GetParentSize(entity);
        Assert.Equal(width, size.X, 3);
        Assert.Equal(height, size.Y, 3);
    }

    /// <summary>Absent viewport preserves reference resolution, while nonpositive reference dimensions become one.</summary>
    [Theory]
    [InlineData(800f, 300f, 800f, 300f)]
    [InlineData(0f, -20f, 1f, 1f)]
    public void MissingViewportUsesSanitizedReference(float width, float height, float expectedWidth, float expectedHeight)
    {
        Assert.Equal(new RectTransformVector2(expectedWidth, expectedHeight), CreateService().GetParentSize(CreateCanvas(width, height, 1, 0)));
    }

    /// <summary>A zero viewport produces a finite empty canvas rather than dividing by zero.</summary>
    [Fact]
    public void ZeroViewportProducesZeroCanvas()
    {
        _window.Size.Value = (0, 0);
        Assert.Equal(new RectTransformVector2(0, 0), CreateService().GetParentSize(CreateCanvas(800, 300, 1, 0.5f)));
    }

    /// <summary>A child uses its parent's computed rect dimensions and finds a canvas through an unadorned ancestor.</summary>
    [Fact]
    public void ParentRectAndAncestorCanvasSupplySize()
    {
        var scene = new Scene("layout");
        _scenes.Scene.Value = scene;
        var canvas = CreateCanvas(800, 300, 1, 0);
        var parent = new GameEntity(2, "parent");
        var child = new GameEntity(3, "child");
        scene.AddEntity(canvas);
        scene.AddEntity(parent);
        scene.AddEntity(child);
        scene.MoveEntity(parent, canvas, 0);
        scene.MoveEntity(child, parent, 0);
        var service = CreateService();
        Assert.Equal(new RectTransformVector2(800, 300), service.GetParentSize(child));

        parent.AddBehaviour(SceneComponentBuilder.RectTransform(RECT_ID, new(new(0, 0), new(0.5f, 1), new(0, 0), new(0, 0), new(-20, -10))));
        Assert.Equal(new RectTransformVector2(380, 290), service.GetParentSize(child));
    }

    /// <summary>Changing pivot preserves the independently known rectangle without mutating the original component.</summary>
    [Fact]
    public void PivotPreservationReturnsNewLayoutWithoutMutation()
    {
        var entity = CreateCanvas(800, 300, 1, 0);
        var layout = new RectTransformLayoutData(new(0, 0), new(0, 0), new(0.25f, 0.75f), new(20, 30), new(80, 40));
        var component = SceneComponentBuilder.RectTransform(RECT_ID, layout);
        entity.AddBehaviour(component);
        var service = CreateService();

        Assert.True(service.TryPreserveRectForPivot(entity, component, 1, 0, out var changed));
        Assert.Equal(new RectTransformVector2(1, 0), changed.Pivot);
        Assert.Equal(new RectTransformVector2(80, 0), changed.AnchoredPosition);
        Assert.Equal(new RectTransformRect(0, 0, 80, 40), RectTransformLayoutCalculator.CalculateRect(new(800, 300), changed));
        Assert.True(service.TryReadLayout(component, out var original));
        Assert.Equal(layout, original);
    }

    /// <summary>Reparenting subtracts the new parent's absolute origin and preserves the child's screen rectangle.</summary>
    [Fact]
    public void ReparentPreservesAbsoluteRect()
    {
        var scene = new Scene("layout");
        _scenes.Scene.Value = scene;
        var parent = new GameEntity(1, "parent");
        parent.AddBehaviour(SceneComponentBuilder.RectTransform(RECT_ID, new(new(0, 0), new(0, 0), new(0, 0), new(100, 50), new(400, 200))));
        var child = new GameEntity(2, "child");
        child.AddBehaviour(SceneComponentBuilder.RectTransform(RECT_ID, new(new(0, 0), new(0, 0), new(0, 0), new(20, 30), new(80, 40))));
        scene.AddEntity(parent);
        scene.AddEntity(child);
        var service = CreateService();

        Assert.True(service.TryPreserveRectForParent(child, parent, out var preserved));
        Assert.Equal(new RectTransformVector2(-80, -20), preserved.AnchoredPosition);
        Assert.Equal(new RectTransformVector2(80, 40), preserved.SizeDelta);
        Assert.Equal(0, child.Transform.Parent);
    }

    /// <summary>Applying a layout updates all vectors and emits exactly one complete-value notification per vector.</summary>
    [Fact]
    public void ApplyLayoutPublishesCompleteVectorsOnce()
    {
        var component = SceneComponentBuilder.RectTransform(RECT_ID, default);
        var desired = new RectTransformLayoutData(new(0.1f, 0.2f), new(0.8f, 0.9f), new(0.3f, 0.7f), new(10, -20), new(80, 40));
        var expectedVectors = new Dictionary<string, RectTransformVector2>
        {
            [EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN] = desired.AnchorMin,
            [EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX] = desired.AnchorMax,
            [EngineBehavioursConstants.RECT_TRANSFORM_PIVOT] = desired.Pivot,
            [EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION] = desired.AnchoredPosition,
            [EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA] = desired.SizeDelta
        };
        var notifications = new List<string>();
        foreach (var property in component.Properties.Values)
        {
            property.ValueChangedEvent += _ =>
            {
                var vector = Assert.IsType<Dictionary<string, SerializedProperty>>(property.Value);
                Assert.Equal(expectedVectors[property.Name].X, vector["x"].Value);
                Assert.Equal(expectedVectors[property.Name].Y, vector["y"].Value);
                notifications.Add(property.Name);
            };
        }
        var service = CreateService();
        service.ApplyLayoutToEditor(component, desired);

        Assert.True(service.TryReadLayout(component, out var actual));
        Assert.Equal(desired, actual);
        Assert.Equal(component.Properties.Keys.Order(), notifications.Order());
        var serialized = service.SerializeVector2(new(3, -4));
        var nested = Assert.IsType<Dictionary<string, object?>>(Assert.Single(serialized).Value);
        Assert.Equal(new[] { "x", "y" }, nested.Keys.Order());
        Assert.Equal(3f, Assert.IsType<Dictionary<string, object?>>(nested["x"])["Value"]);
        Assert.Equal(-4f, Assert.IsType<Dictionary<string, object?>>(nested["y"])["Value"]);
    }

    private RectTransformLayoutService CreateService() => new(_registry, _scenes, _window);

    private static GameEntity CreateCanvas(float width, float height, int mode, float match)
    {
        var entity = new GameEntity(1, "canvas");
        var component = new BehaviourComponent(CANVAS_ID);
        component.AddProperty(SceneComponentBuilder.Vector(EngineBehavioursConstants.CANVAS_REFERENCE_RESOLUTION, ("x", width), ("y", height)));
        component.AddProperty(SerializedPropertyBuilder.Integer(EngineBehavioursConstants.CANVAS_SCALE_MODE, mode));
        component.AddProperty(new(EngineBehavioursConstants.CANVAS_MATCH_WIDTH_OR_HEIGHT, ReiEditor.Models.Services.Assets.Scripting.Serialization.Types.SerializedTypeEnum.Float, match, "float", null));
        entity.AddBehaviour(component);
        return entity;
    }
}
