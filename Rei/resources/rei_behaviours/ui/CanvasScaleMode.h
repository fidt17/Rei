#pragma once

namespace rei::ui
{
    SERIALIZABLE_ENUM(CanvasScaleMode)
    {
        ConstantPixelSize = 0,
        ScaleWithScreenSize = 1,
    };
}
