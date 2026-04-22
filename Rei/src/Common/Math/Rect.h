#pragma once

#include "Core.h"

#include "glm/fwd.hpp"
#include "glm/vec2.hpp"

namespace rei::math
{
    struct Rect
    {
        glm::vec2 Min = {};
        glm::vec2 Max = {};

        REI_API glm::vec2 GetSize() const;
        REI_API glm::vec2 GetCenter() const;
    };
}
