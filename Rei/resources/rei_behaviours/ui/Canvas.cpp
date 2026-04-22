#include "pch.h"

#include "Canvas.h"

namespace rei::ui
{
    const math::Vector3& Canvas::GetReferenceResolution() const
    {
        return _referenceResolution;
    }

    CanvasScaleMode Canvas::GetScaleMode() const
    {
        return _scaleMode;
    }

    f32 Canvas::GetMatchWidthOrHeight() const
    {
        return _matchWidthOrHeight;
    }

    f32 Canvas::GetPixelsPerUnit() const
    {
        return _pixelsPerUnit;
    }
}
