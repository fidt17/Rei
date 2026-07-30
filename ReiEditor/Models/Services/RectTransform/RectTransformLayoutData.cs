namespace ReiEditor.Models.Services.RectTransform;

public readonly record struct RectTransformLayoutData(
    RectTransformVector2 AnchorMin,
    RectTransformVector2 AnchorMax,
    RectTransformVector2 Pivot,
    RectTransformVector2 AnchoredPosition,
    RectTransformVector2 SizeDelta);
