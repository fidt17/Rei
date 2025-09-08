#include "pch.h"

#include "Transform.h"

void rei::transformation::Transform::Reset()
{
    _position = math::Vector3(0, 0, 0);
    _rotation = math::Vector3(0, 0, 0);
    _scale = math::Vector3(1, 1, 1);
}

rei::math::Vector3& rei::transformation::Transform::GetPosition()
{
    return _position;
}

rei::math::Vector3& rei::transformation::Transform::GetScale()
{
    return _scale;
}

rei::math::Vector3& rei::transformation::Transform::GetRotation()
{
    return _rotation;
}

glm::mat4 rei::transformation::Transform::CalculateModelMatrix() const
{
    return GetTransformationMatrix(_position, _rotation, _scale);
}

rei::math::Vector3 rei::transformation::Transform::GetForward() const
{
    return math::Vector3::Forward().Transform(GetRotationMatrix(_rotation));
}

rei::math::Vector3 rei::transformation::Transform::GetRight() const
{
    return math::Vector3::Right().Transform(GetRotationMatrix(_rotation));
}

rei::math::Vector3 rei::transformation::Transform::GetUp() const
{
    return math::Vector3::Up().Transform(GetRotationMatrix(_rotation));
}

rei::transformation::Transform::operator std::string() const
{
    auto f = std::format("P: {}\nR: {}\nS: {}", std::string(_position), std::string(_rotation), std::string(_scale));
    return std::string(f);
}
