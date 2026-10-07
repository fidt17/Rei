#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class SpotLight : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(SpotLight)

        SERIALIZE f32 _strength = 1;
        SERIALIZE f32 _range = 10;
        SERIALIZE Color _color;
        REI_RANGE(0.0f, 179.0f)
        SERIALIZE f32 _innerAngle = 30;
        REI_RANGE(0.0f, 179.0f)
        SERIALIZE f32 _outerAngle = 50;

    public:
        REI_API void OnGizmosSelected() override;

        REI_API f32 GetStrength() const;
        REI_API f32 GetRange() const;
        REI_API Color GetColor() const;
        // Full cone widths in degrees; effective inner angle never exceeds outer angle.
        REI_API f32 GetInnerAngle() const;
        REI_API f32 GetOuterAngle() const;
        REI_API math::Vector3 GetWorldDirection() const;

        REI_API void SetStrength(f32 value);
        REI_API void SetRange(f32 value);
        REI_API void SetColor(Color value);
        REI_API void SetInnerAngle(f32 value);
        REI_API void SetOuterAngle(f32 value);
    };
}
EXPORT_COMPONENT(rei::render::SpotLight)
