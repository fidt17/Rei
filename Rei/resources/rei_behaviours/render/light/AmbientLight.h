#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class AmbientLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(AmbientLight)
        SERIALIZE f32 _strength = 1;
        SERIALIZE Color _color;

    public:
        REI_API f32 GetStrength() const;
        REI_API Color GetColor() const;
    };
}
EXPORT_COMPONENT(rei::render::AmbientLight);
