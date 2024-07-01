#include "pch.h"
#include "PointLight.h"

f32 rei::behaviour::PointLight::GetStrength() const
{
    return _strength;
}

rei::render::Color rei::behaviour::PointLight::GetColor() const
{
    return _color;
}

void rei::behaviour::PointLight::SetStrength(const f32 value)
{
    _strength = value;
}

void rei::behaviour::PointLight::SetColor(const render::Color value)
{
    _color = value;
}
