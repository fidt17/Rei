#pragma once

#include "Core.h"

#include "Vector2.h"

namespace rei::math
{
    struct Rect
    {
        Vector2 Min = {};
        Vector2 Max = {};

        REI_API Vector2 GetSize() const;
        REI_API Vector2 GetCenter() const;
    };
}
