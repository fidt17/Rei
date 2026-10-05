#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class PointLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(PointLight)

        SERIALIZE f32 _strength = 1;
        SERIALIZE f32 _range = 10;
        SERIALIZE Color _color;

    public:
        REI_API f32 GetStrength() const;
        REI_API f32 GetRange() const;
        REI_API Color GetColor() const;

        REI_API void SetStrength(f32 value);
        // World-space distance; zero disables influence, positive values are at least 0.01.
        REI_API void SetRange(f32 value);
        REI_API void SetColor(Color value);
    };
}
EXPORT_COMPONENT(rei::render::PointLight)
