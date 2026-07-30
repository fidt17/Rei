namespace ReiEditor.Models.Services.RectTransform;

public static class RectTransformLayoutCalculator
{
    public static RectTransformRect CalculateRect(RectTransformVector2 parentSize, RectTransformLayoutData data)
    {
        return CalculateRect(
            parentSize,
            data.AnchorMin.X,
            data.AnchorMin.Y,
            data.AnchorMax.X,
            data.AnchorMax.Y,
            data.Pivot.X,
            data.Pivot.Y,
            data.AnchoredPosition.X,
            data.AnchoredPosition.Y,
            data.SizeDelta.X,
            data.SizeDelta.Y);
    }

    public static RectTransformRect CalculateRect(
        RectTransformVector2 parentSize,
        float anchorMinX,
        float anchorMinY,
        float anchorMaxX,
        float anchorMaxY,
        float pivotX,
        float pivotY,
        float anchoredPositionX,
        float anchoredPositionY,
        float sizeDeltaX,
        float sizeDeltaY)
    {
        var anchorSpanX = parentSize.X * (anchorMaxX - anchorMinX);
        var anchorSpanY = parentSize.Y * (anchorMaxY - anchorMinY);
        var anchorReferenceX = parentSize.X * (anchorMinX + (anchorMaxX - anchorMinX) * pivotX);
        var anchorReferenceY = parentSize.Y * (anchorMinY + (anchorMaxY - anchorMinY) * pivotY);
        var width = anchorSpanX + sizeDeltaX;
        var height = anchorSpanY + sizeDeltaY;
        var pivotPositionX = anchorReferenceX + anchoredPositionX;
        var pivotPositionY = anchorReferenceY + anchoredPositionY;

        return new RectTransformRect(
            pivotPositionX - pivotX * width,
            pivotPositionY - pivotY * height,
            pivotPositionX + (1f - pivotX) * width,
            pivotPositionY + (1f - pivotY) * height);
    }

    public static RectTransformLayoutValues CalculateValuesForRect(
        RectTransformVector2 parentSize,
        RectTransformRect rect,
        float anchorMinX,
        float anchorMinY,
        float anchorMaxX,
        float anchorMaxY,
        float pivotX,
        float pivotY)
    {
        var anchorSpanX = parentSize.X * (anchorMaxX - anchorMinX);
        var anchorSpanY = parentSize.Y * (anchorMaxY - anchorMinY);
        var anchorReferenceX = parentSize.X * (anchorMinX + (anchorMaxX - anchorMinX) * pivotX);
        var anchorReferenceY = parentSize.Y * (anchorMinY + (anchorMaxY - anchorMinY) * pivotY);
        var pivotPositionX = rect.MinX + pivotX * rect.Width;
        var pivotPositionY = rect.MinY + pivotY * rect.Height;

        return new RectTransformLayoutValues(
            pivotPositionX - anchorReferenceX,
            pivotPositionY - anchorReferenceY,
            rect.Width - anchorSpanX,
            rect.Height - anchorSpanY);
    }
}
