using System;
using System.Collections.Generic;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Models.Services.Windows.Playmode;

namespace ReiEditor.Models.Services.RectTransform;

public sealed class RectTransformLayoutService : IRectTransformLayoutService
{
    private const float DEFAULT_PARENT_WIDTH = 1920f;
    private const float DEFAULT_PARENT_HEIGHT = 1080f;

    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly IEngineWindowController _engineWindowController;

    public RectTransformLayoutService(
        IBehaviourRegistry behaviourRegistry,
        ISceneManagementService sceneManagementService,
        IEngineWindowController engineWindowController)
    {
        _behaviourRegistry = behaviourRegistry;
        _sceneManagementService = sceneManagementService;
        _engineWindowController = engineWindowController;
    }

    public RectTransformVector2 GetParentSize(GameEntity entity)
    {
        var parent = GetParent(entity);
        if (parent == null) return GetCanvasSize(entity);

        return TryGetEntityRectSize(parent, out var size)
            ? size
            : GetCanvasSize(parent);
    }

    public bool TryGetRectTransform(GameEntity entity, out BehaviourComponent rectTransform)
    {
        rectTransform = GetBehaviour(entity, EngineBehavioursConstants.RECT_TRANSFORM)!;
        return rectTransform != null;
    }

    public bool TryReadLayout(BehaviourComponent rectTransform, out RectTransformLayoutData data)
    {
        data = default;
        if (!HasRectTransformProperties(rectTransform)) return false;

        data = new RectTransformLayoutData(
            GetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN),
            GetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX),
            GetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_PIVOT),
            GetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION),
            GetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA));
        return true;
    }

    public bool TryPreserveRectForPivot(GameEntity entity, BehaviourComponent rectTransform, float pivotX, float pivotY, out RectTransformLayoutData preservedLayout)
    {
        preservedLayout = default;
        if (!TryReadLayout(rectTransform, out var layout)) return false;

        var parentSize = GetParentSize(entity);
        var rect = RectTransformLayoutCalculator.CalculateRect(parentSize, layout);
        var values = RectTransformLayoutCalculator.CalculateValuesForRect(
            parentSize,
            rect,
            layout.AnchorMin.X,
            layout.AnchorMin.Y,
            layout.AnchorMax.X,
            layout.AnchorMax.Y,
            pivotX,
            pivotY);

        preservedLayout = layout with
        {
            Pivot = new RectTransformVector2(pivotX, pivotY),
            AnchoredPosition = new RectTransformVector2(values.AnchoredPositionX, values.AnchoredPositionY),
            SizeDelta = new RectTransformVector2(values.SizeDeltaX, values.SizeDeltaY)
        };
        return true;
    }

    public bool TryPreserveRectForParent(GameEntity entity, GameEntity? newParent, out RectTransformLayoutData preservedLayout)
    {
        preservedLayout = default;
        if (!TryGetRectTransform(entity, out var rectTransform)) return false;
        if (!TryReadLayout(rectTransform, out var layout)) return false;
        if (!TryGetAbsoluteRect(entity, out var absoluteRect)) return false;

        var newParentRect = GetParentAbsoluteRect(entity, newParent);
        var localRect = OffsetRect(absoluteRect, -newParentRect.MinX, -newParentRect.MinY);
        var parentSize = new RectTransformVector2(newParentRect.Width, newParentRect.Height);
        var values = RectTransformLayoutCalculator.CalculateValuesForRect(
            parentSize,
            localRect,
            layout.AnchorMin.X,
            layout.AnchorMin.Y,
            layout.AnchorMax.X,
            layout.AnchorMax.Y,
            layout.Pivot.X,
            layout.Pivot.Y);

        preservedLayout = layout with
        {
            AnchoredPosition = new RectTransformVector2(values.AnchoredPositionX, values.AnchoredPositionY),
            SizeDelta = new RectTransformVector2(values.SizeDeltaX, values.SizeDeltaY)
        };
        return true;
    }

    public void ApplyLayoutToEditor(BehaviourComponent rectTransform, RectTransformLayoutData layout)
    {
        SetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN, layout.AnchorMin);
        SetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX, layout.AnchorMax);
        SetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_PIVOT, layout.Pivot);
        SetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION, layout.AnchoredPosition);
        SetVector2(rectTransform, EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA, layout.SizeDelta);
    }

    public Dictionary<string, object?> SerializeVector2(RectTransformVector2 value)
    {
        return new Dictionary<string, object?>
        {
            {
                "Value",
                new Dictionary<string, object?>
                {
                    { "x", new Dictionary<string, object?> { { "Value", value.X } } },
                    { "y", new Dictionary<string, object?> { { "Value", value.Y } } }
                }
            }
        };
    }

    private bool TryGetAbsoluteRect(GameEntity entity, out RectTransformRect rect)
    {
        rect = default;
        if (TryGetCanvasSize(entity, out var canvasSize))
        {
            rect = new RectTransformRect(0f, 0f, canvasSize.X, canvasSize.Y);
            return true;
        }

        if (!TryGetRectTransform(entity, out var rectTransform)) return false;
        if (!TryReadLayout(rectTransform, out var layout)) return false;

        var parentRect = GetCurrentParentAbsoluteRect(entity);
        var localRect = RectTransformLayoutCalculator.CalculateRect(new RectTransformVector2(parentRect.Width, parentRect.Height), layout);
        rect = OffsetRect(localRect, parentRect.MinX, parentRect.MinY);
        return true;
    }

    private RectTransformRect GetCurrentParentAbsoluteRect(GameEntity entity)
    {
        return GetParentAbsoluteRect(entity, GetParent(entity));
    }

    private RectTransformRect GetParentAbsoluteRect(GameEntity contextEntity, GameEntity? parent)
    {
        if (parent == null)
        {
            var canvasSize = GetCanvasSize(contextEntity);
            return new RectTransformRect(0f, 0f, canvasSize.X, canvasSize.Y);
        }

        return TryGetAbsoluteRect(parent, out var rect)
            ? rect
            : CreateCanvasRect(parent);
    }

    private RectTransformRect CreateCanvasRect(GameEntity entity)
    {
        var canvasSize = GetCanvasSize(entity);
        return new RectTransformRect(0f, 0f, canvasSize.X, canvasSize.Y);
    }

    private bool TryGetEntityRectSize(GameEntity entity, out RectTransformVector2 size)
    {
        if (TryGetCanvasSize(entity, out size)) return true;
        if (!TryGetAbsoluteRect(entity, out var rect)) return false;

        size = new RectTransformVector2(rect.Width, rect.Height);
        return true;
    }

    private RectTransformVector2 GetCanvasSize(GameEntity entity)
    {
        return TryFindCanvasSize(entity, out var size)
            ? size
            : new RectTransformVector2(DEFAULT_PARENT_WIDTH, DEFAULT_PARENT_HEIGHT);
    }

    private bool TryFindCanvasSize(GameEntity entity, out RectTransformVector2 size)
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        GameEntity? current = entity;
        while (current != null)
        {
            if (TryGetCanvasSize(current, out size)) return true;
            current = current.Transform.HasParent() ? scene?.GetById(current.Transform.Parent) : null;
        }

        size = default;
        return false;
    }

    private bool TryGetCanvasSize(GameEntity entity, out RectTransformVector2 size)
    {
        var canvas = GetBehaviour(entity, EngineBehavioursConstants.CANVAS);
        if (canvas != null && canvas.HasProperty(EngineBehavioursConstants.CANVAS_REFERENCE_RESOLUTION))
        {
            size = CalculateCanvasSize(canvas);
            return true;
        }

        size = default;
        return false;
    }

    private RectTransformVector2 CalculateCanvasSize(BehaviourComponent canvas)
    {
        var referenceSize = GetVector2(canvas, EngineBehavioursConstants.CANVAS_REFERENCE_RESOLUTION);
        var referenceWidth = referenceSize.X <= 0f ? 1f : referenceSize.X;
        var referenceHeight = referenceSize.Y <= 0f ? 1f : referenceSize.Y;
        var viewportSize = _engineWindowController.ViewportSize.Value;
        if (viewportSize == null) return new RectTransformVector2(referenceWidth, referenceHeight);

        var viewportWidth = viewportSize.Value.Width;
        var viewportHeight = viewportSize.Value.Height;
        var scaleFactor = CalculateCanvasScaleFactor(canvas, viewportWidth, viewportHeight, referenceWidth, referenceHeight);

        return new RectTransformVector2(viewportWidth / scaleFactor, viewportHeight / scaleFactor);
    }

    private GameEntity? GetParent(GameEntity entity)
    {
        var scene = _sceneManagementService.CurrentScene.Value;
        return entity.Transform.HasParent() ? scene?.GetById(entity.Transform.Parent) : null;
    }

    private BehaviourComponent? GetBehaviour(GameEntity entity, string behaviourName)
    {
        var id = _behaviourRegistry.GetIdByName(behaviourName);
        return entity.GetBehaviour(id);
    }

    private static RectTransformRect OffsetRect(RectTransformRect rect, float offsetX, float offsetY)
    {
        return new RectTransformRect(rect.MinX + offsetX, rect.MinY + offsetY, rect.MaxX + offsetX, rect.MaxY + offsetY);
    }

    private static bool HasRectTransformProperties(BehaviourComponent component)
    {
        return component.HasProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MIN)
               && component.HasProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHOR_MAX)
               && component.HasProperty(EngineBehavioursConstants.RECT_TRANSFORM_PIVOT)
               && component.HasProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)
               && component.HasProperty(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA);
    }

    private static void SetVector2(BehaviourComponent component, string propertyName, RectTransformVector2 value)
    {
        var property = component.GetProperty(propertyName);
        if (property.Value is not IReadOnlyDictionary<string, SerializedProperty> nestedProperties) return;

        if (nestedProperties.TryGetValue("x", out var xProperty))
        {
            xProperty.SetValueWithoutTriggeringChangedEvent(value.X);
        }

        if (nestedProperties.TryGetValue("y", out var yProperty))
        {
            yProperty.SetValueWithoutTriggeringChangedEvent(value.Y);
        }

        property.TriggerChangedEvent();
    }

    private static RectTransformVector2 GetVector2(BehaviourComponent component, string propertyName)
    {
        var property = component.GetProperty(propertyName);
        if (property.Value is not IReadOnlyDictionary<string, SerializedProperty> nestedProperties) return default;

        var x = nestedProperties.TryGetValue("x", out var xProperty)
            ? Convert.ToSingle(xProperty.Value ?? 0f)
            : 0f;
        var y = nestedProperties.TryGetValue("y", out var yProperty)
            ? Convert.ToSingle(yProperty.Value ?? 0f)
            : 0f;

        return new RectTransformVector2(x, y);
    }

    private static float CalculateCanvasScaleFactor(BehaviourComponent canvas, float viewportWidth, float viewportHeight, float referenceWidth, float referenceHeight)
    {
        var scaleMode = GetInt(canvas, EngineBehavioursConstants.CANVAS_SCALE_MODE, defaultValue: 1);
        if (scaleMode == 0) return 1f;

        var match = Math.Clamp(GetFloat(canvas, EngineBehavioursConstants.CANVAS_MATCH_WIDTH_OR_HEIGHT, defaultValue: 0f), 0f, 1f);
        var widthScale = viewportWidth / referenceWidth;
        var heightScale = viewportHeight / referenceHeight;
        return Math.Max(0.0001f, widthScale + (heightScale - widthScale) * match);
    }

    private static float GetFloat(BehaviourComponent component, string propertyName, float defaultValue)
    {
        if (!component.HasProperty(propertyName)) return defaultValue;

        return Convert.ToSingle(component.GetProperty(propertyName).Value ?? defaultValue);
    }

    private static int GetInt(BehaviourComponent component, string propertyName, int defaultValue)
    {
        if (!component.HasProperty(propertyName)) return defaultValue;

        var value = component.GetProperty(propertyName).Value;
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => Convert.ToInt32(value ?? defaultValue)
        };
    }
}
