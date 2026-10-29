#include "pch.h"

#include "RectTransform.h"

namespace rei::ui
{
    math::Vector2& RectTransform::GetAnchorMin() { return _anchorMin; }

    const math::Vector2& RectTransform::GetAnchorMin() const
    {
        return _anchorMin;
    }

    math::Vector2& RectTransform::GetAnchorMax() { return _anchorMax; }

    const math::Vector2& RectTransform::GetAnchorMax() const
    {
        return _anchorMax;
    }

    math::Vector2& RectTransform::GetPivot() { return _pivot; }

    const math::Vector2& RectTransform::GetPivot() const
    {
        return _pivot;
    }

    math::Vector2& RectTransform::GetAnchoredPosition()
    {
        return _anchoredPosition;
    }

    const math::Vector2& RectTransform::GetAnchoredPosition() const
    {
        return _anchoredPosition;
    }

    math::Vector2& RectTransform::GetSizeDelta()
    {
        return _sizeDelta;
    }

    const math::Vector2& RectTransform::GetSizeDelta() const
    {
        return _sizeDelta;
    }
}
