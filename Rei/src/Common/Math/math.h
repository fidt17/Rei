#pragma once

namespace rei::math
{
    template <typename T>
    T lerp(T a, T b, T t)
    {
        return a + t * (b - a);
    }
}
