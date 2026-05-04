#pragma once

#include "Common/Math/Vector2.h"

namespace rei::ui
{
    class RectTransform : public Behaviour
    {
        BEHAVIOUR_BODY(RectTransform)

        SERIALIZE math::Vector2 _anchorMin = math::Vector2(0.5f, 0.5f);
        SERIALIZE math::Vector2 _anchorMax = math::Vector2(0.5f, 0.5f);
        SERIALIZE math::Vector2 _pivot = math::Vector2(0.5f, 0.5f);
        SERIALIZE math::Vector2 _anchoredPosition = math::Vector2(0, 0);
        SERIALIZE math::Vector2 _sizeDelta = math::Vector2(100, 100);

    public:
        REI_API const math::Vector2& GetAnchorMin() const;
        REI_API const math::Vector2& GetAnchorMax() const;
        REI_API const math::Vector2& GetPivot() const;
        REI_API math::Vector2& GetAnchoredPosition();
        REI_API const math::Vector2& GetAnchoredPosition() const;
        REI_API math::Vector2& GetSizeDelta();
        REI_API const math::Vector2& GetSizeDelta() const;
    };
}

EXPORT_COMPONENT(rei::ui::RectTransform)
