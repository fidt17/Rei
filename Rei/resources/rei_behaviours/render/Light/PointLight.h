#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::behaviour
{
    class PointLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(PointLight)

        SERIALIZE f32 _strength;
        SERIALIZE render::Color _color;

    public:
        REI_API f32 GetStrength() const;
        REI_API render::Color GetColor() const;
    };
}
EXPORT_COMPONENT(rei::behaviour::PointLight)
