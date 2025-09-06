#include "pch.h"
#include "Ray.h"

rei::math::Ray::operator std::string() const
{
    return "Origin=" + std::string(Origin) + ", Direction=" + std::string(Direction);
}
