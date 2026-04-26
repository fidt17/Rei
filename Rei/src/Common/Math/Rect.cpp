#include "pch.h"

#include "Rect.h"

namespace rei::math
{
    Vector2 Rect::GetSize() const
    {
        return Max - Min;
    }

    Vector2 Rect::GetCenter() const
    {
        return (Min + Max) * 0.5f;
    }
}
