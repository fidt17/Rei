#include "pch.h"
#include "Transform.h"

#include "glm/trigonometric.hpp"
#include "glm/ext/quaternion_geometric.hpp"

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

rei::math::Vector3 rei::transformation::Transform::GetForward() const
{
    glm::vec3 forward;

    const float yaw = _rotation.x - 90.0f;
    const float pitch = _rotation.y;

    forward.x = cos(glm::radians(yaw)) * cos(glm::radians(pitch));
    forward.y = sin(glm::radians(pitch));
    forward.z = -sin(glm::radians(yaw)) * cos(glm::radians(pitch));

    forward = normalize(forward);

    return math::Vector3(forward);
}

rei::math::Vector3 rei::transformation::Transform::GetRight() const
{
    const glm::vec3 forward = GetForward();
    const glm::vec3 up = GetUp();
    
    return math::Vector3(glm::normalize(glm::cross(forward, up)));
}

rei::math::Vector3 rei::transformation::Transform::GetUp() const
{
    return math::Vector3(0, 1.0f, 0);
}
