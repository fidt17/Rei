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
