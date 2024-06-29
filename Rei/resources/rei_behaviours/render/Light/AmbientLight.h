#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class AmbientLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(AmbientLight)
        SERIALIZE f32 _strength;
        SERIALIZE Color _color;

    public:
        REI_API f32 GetStrength() const { return _strength; }
        REI_API Color GetColor() const { return _color; }
    };
}
EXPORT_COMPONENT(rei::render::AmbientLight);
