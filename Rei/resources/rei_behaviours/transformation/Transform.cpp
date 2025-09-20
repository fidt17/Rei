#include "pch.h"

#include "Transform.h"

#include "glm/ext/quaternion_trigonometric.hpp"

namespace rei::transformation
{
    void Transform::Reset()
    {
        _position = math::Vector3(0, 0, 0);
        _rotation = math::Vector3(0, 0, 0);
        _scale = math::Vector3(1, 1, 1);
    }

    void Transform::AfterREI_SET()
    {
        _quaternion = GetQuaternion(_rotation);
    }

    void Transform::BeforeREI_GET()
    {
        _rotation = math::GetEulerAngles(_quaternion);
    }

    math::Vector3& Transform::GetPosition()
    {
        return _position;
    }

    math::Vector3& Transform::GetScale()
    {
        return _scale;
    }

    const glm::quat& Transform::GetRotation() const
    {
        return _quaternion;
    }

    void Transform::Translate(const math::Vector3& translation)
    {
        _position += translation;
    }

    void Transform::SetRotation(const math::Vector3& eulerAngles)
    {
        SetRotation(GetQuaternion(eulerAngles));
    }

    void Transform::SetRotation(const glm::quat& quaternion)
    {
        _quaternion = quaternion;
        _rotation = math::GetEulerAngles(_quaternion);
    }

    void Transform::RotateWorld(f32 angle, const math::Vector3& axis)
    {
        const f32 angleRad = glm::radians(angle);

        const glm::quat delta = angleAxis(angleRad, glm::vec3(math::Vector3::Normalize(axis)));

        _quaternion = delta * _quaternion;

        _quaternion = normalize(_quaternion);
        _rotation = math::GetEulerAngles(_quaternion);
    }

    void Transform::RotateLocal(const f32 angle, const math::Vector3& axis)
    {
        const f32 angleRad = glm::radians(angle);

        const glm::quat delta = angleAxis(angleRad, glm::vec3(math::Vector3::Normalize(axis)));

        _quaternion = _quaternion * delta;

        _quaternion = normalize(_quaternion);
        _rotation = math::GetEulerAngles(_quaternion);
    }

    glm::mat4 Transform::CalculateModelMatrix() const
    {
        return GetTransformationMatrix(_position, _quaternion, _scale);
    }

    math::Vector3 Transform::GetForward() const
    {
        return _quaternion * glm::vec3(0,0,1);
    }

    math::Vector3 Transform::GetRight() const
    {
        return _quaternion * glm::vec3(1,0,0);
    }

    math::Vector3 Transform::GetUp() const
    {
        return _quaternion * glm::vec3(0,1,0);
    }

    Transform::operator std::string() const
    {
        auto f = std::format("P: {}\nR: {}\nS: {}", std::string(_position), std::string(_rotation), std::string(_scale));
        return std::string(f);
    }
}
