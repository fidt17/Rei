#pragma once

#include "Common/Math/Vector3.h"
#include "rei_behaviours/ui/CanvasScaleMode.h"

namespace rei::ui
{
    class Canvas : public Behaviour
    {
        BEHAVIOUR_BODY(Canvas)

        SERIALIZE math::Vector3 _referenceResolution = math::Vector3(1920, 1080, 0);
        SERIALIZE CanvasScaleMode _scaleMode = ScaleWithScreenSize;
        SERIALIZE f32 _matchWidthOrHeight = 0.0f;
        SERIALIZE f32 _pixelsPerUnit = 100.0f;
    };
}

EXPORT_COMPONENT(rei::ui::Canvas)
