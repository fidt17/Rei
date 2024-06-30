#pragma once

namespace rei::transformation
{
    class Transform : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Transform)

        SERIALIZE math::Vector3 _position;
        SERIALIZE math::Vector3 _rotation;
        SERIALIZE math::Vector3 _scale;

    public:
        REI_API math::Vector3& GetPosition();
        REI_API math::Vector3& GetScale();
        REI_API math::Vector3& GetRotation();

        REI_API math::Vector3 GetForward() const;
        REI_API math::Vector3 GetRight() const;
        REI_API math::Vector3 GetUp() const;

        REI_API operator std::string() const;
    };
}
EXPORT_COMPONENT(rei::transformation::Transform)
