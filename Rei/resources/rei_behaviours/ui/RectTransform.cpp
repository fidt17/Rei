#include "pch.h"

#include "RectTransform.h"

namespace rei::ui
{
    const math::Vector3& RectTransform::GetAnchorMin() const
    {
        return _anchorMin;
    }

    const math::Vector3& RectTransform::GetAnchorMax() const
    {
        return _anchorMax;
    }

    const math::Vector3& RectTransform::GetPivot() const
    {
        return _pivot;
    }

    const math::Vector3& RectTransform::GetAnchoredPosition() const
    {
        return _anchoredPosition;
    }

    const math::Vector3& RectTransform::GetSizeDelta() const
    {
        return _sizeDelta;
    }
}
