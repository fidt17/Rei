#include "pch.h"
#include "AmbientLight.h"

f32 rei::render::AmbientLight::GetStrength() const
{
    return _strength;
}

rei::render::Color rei::render::AmbientLight::GetColor() const
{
    return _color;
}
