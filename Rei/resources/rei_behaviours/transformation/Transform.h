#pragma once
#include "glm/detail/type_quat.hpp"

namespace rei::transformation
{
    class Transform : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Transform)
        SERIALIZE math::Vector3 _position;
        SERIALIZE math::Vector3 _rotation;
        SERIALIZE math::Vector3 _scale;

        glm::quat _quaternion;

    public:
        REI_API void Reset();

        REI_API void AfterREI_SET() override;
        REI_API void BeforeREI_GET() override;

        REI_API math::Vector3& GetPosition();
        REI_API math::Vector3& GetScale();
        REI_API const glm::quat& GetRotation() const;

        REI_API void Translate(const math::Vector3& translation);

        REI_API void SetRotation(const math::Vector3& eulerAngles);
        REI_API void SetRotation(const glm::quat& quaternion);

        REI_API void RotateWorld(f32 angle, const math::Vector3& axis);
        REI_API void RotateLocal(f32 angle, const math::Vector3& axis);

        REI_API glm::mat4 CalculateModelMatrix() const;

        REI_API math::Vector3 GetForward() const;
        REI_API math::Vector3 GetRight() const;
        REI_API math::Vector3 GetUp() const;

        REI_API operator std::string() const;
    };
}
EXPORT_COMPONENT(rei::transformation::Transform)
