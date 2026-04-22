#include "pch.h"

#include "Rect.h"

namespace rei::math
{
    glm::vec2 Rect::GetSize() const
    {
        return Max - Min;
    }

    glm::vec2 Rect::GetCenter() const
    {
        return (Min + Max) * 0.5f;
    }
}
