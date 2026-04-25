#pragma once

#include "Common/Math/Vector3.h"
#include "rei_behaviours/ui/CanvasScaleMode.h"

namespace rei::ui
{
    class Canvas : public Behaviour
    {
        REQUIRE_COMPONENT(RectTransform)
        BEHAVIOUR_BODY(Canvas)

        SERIALIZE math::Vector3 _referenceResolution = math::Vector3(1920, 1080, 0);
        SERIALIZE CanvasScaleMode _scaleMode = ScaleWithScreenSize;
        SERIALIZE f32 _matchWidthOrHeight = 0.0f;
        SERIALIZE f32 _pixelsPerUnit = 100.0f;

    public:
        REI_API const math::Vector3& GetReferenceResolution() const;
        REI_API CanvasScaleMode GetScaleMode() const;
        REI_API f32 GetMatchWidthOrHeight() const;
        REI_API f32 GetPixelsPerUnit() const;
    };
}

EXPORT_COMPONENT(rei::ui::Canvas)
