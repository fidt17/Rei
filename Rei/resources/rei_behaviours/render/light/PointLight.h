#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::behaviour
{
    class PointLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(PointLight)

        SERIALIZE f32 _strength = 1;
        SERIALIZE render::Color _color;

    public:
        REI_API f32 GetStrength() const;
        REI_API render::Color GetColor() const;

        REI_API void SetStrength(f32 value);
        REI_API void SetColor(render::Color value);
    };
}
EXPORT_COMPONENT(rei::behaviour::PointLight)
