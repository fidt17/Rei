#include "pch.h"
#include "PointLight.h"

#include <algorithm>
#include <cmath>

namespace
{
    f32 ClampLightRange(const f32 value)
    {
        if (!std::isfinite(value) || value <= 0) return 0;
        return std::max(value, 0.01f);
    }
}

f32 rei::render::PointLight::GetStrength() const
{
    return _strength;
}

f32 rei::render::PointLight::GetRange() const
{
    return ClampLightRange(_range);
}

rei::render::Color rei::render::PointLight::GetColor() const
{
    return _color;
}

void rei::render::PointLight::SetStrength(const f32 value)
{
    _strength = value;
}

void rei::render::PointLight::SetRange(const f32 value)
{
    _range = ClampLightRange(value);
}

void rei::render::PointLight::SetColor(const Color value)
{
    _color = value;
}
