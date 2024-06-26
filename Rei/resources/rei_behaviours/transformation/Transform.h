#pragma once

namespace rei::transformation
{
    class Transform : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Transform)

        SERIALIZE math::Vector3 _position;
        SERIALIZE math::Vector3 _scale;
        SERIALIZE math::Vector3 _rotation;

    public:
        REI_API math::Vector3& GetPosition();
        REI_API math::Vector3& GetScale();
        REI_API math::Vector3& GetRotation();
    };
}
EXPORT_COMPONENT(rei::transformation::Transform)
