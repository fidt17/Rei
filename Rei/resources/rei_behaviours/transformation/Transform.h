#pragma once
#include "glm/fwd.hpp"

namespace rei::transformation
{
    class Transform : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Transform)

        SERIALIZE math::Vector3 _position = math::Vector3(0,0,0);
        SERIALIZE math::Vector3 _rotation = math::Vector3(0, 0, 0);
        SERIALIZE math::Vector3 _scale = math::Vector3(1, 1, 1);

    public:
        REI_API math::Vector3& GetPosition();
        REI_API math::Vector3& GetScale();
        REI_API math::Vector3& GetRotation();
        REI_API glm::mat4 CalculateModelMatrix() const;

        REI_API math::Vector3 GetForward() const;
        REI_API math::Vector3 GetRight() const;
        REI_API math::Vector3 GetUp() const;

        REI_API operator std::string() const;
    };
}
EXPORT_COMPONENT(rei::transformation::Transform)
