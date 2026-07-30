namespace ReiEditor.Models.Services.RectTransform;

public readonly record struct RectTransformVector2(float X, float Y);

public readonly record struct RectTransformLayoutValues(float AnchoredPositionX, float AnchoredPositionY, float SizeDeltaX, float SizeDeltaY);

public readonly record struct RectTransformRect(float MinX, float MinY, float MaxX, float MaxY)
{
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
}
