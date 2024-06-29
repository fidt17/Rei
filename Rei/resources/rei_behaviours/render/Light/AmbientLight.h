#pragma once

namespace rei::render
{
    class AmbientLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(AmbientLight)
        SERIALIZE f32 _strength;
        SERIALIZE math::Vector3 _color;

    public:
        REI_API f32 GetStrength() const { return _strength; }
        REI_API math::Vector3 GetColor() const { return _color; }
    };
}
EXPORT_COMPONENT(rei::render::AmbientLight);
