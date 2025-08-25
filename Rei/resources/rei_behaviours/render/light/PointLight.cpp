#include "pch.h"
#include "PointLight.h"

f32 rei::render::PointLight::GetStrength() const
{
    return _strength;
}

rei::render::Color rei::render::PointLight::GetColor() const
{
    return _color;
}

void rei::render::PointLight::SetStrength(const f32 value)
{
    _strength = value;
}

void rei::render::PointLight::SetColor(const Color value)
{
    _color = value;
}
