#include "pch.h"
#include "Transform.h"

#include "glm/trigonometric.hpp"
#include "glm/ext/quaternion_geometric.hpp"

void rei::transformation::Transform::Reset()
{
    _position = math::Vector3(0,0,0);
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
    auto model = glm::mat4(1.0f);
    model = translate(model, glm::vec3(_position));
    model = rotate(model, glm::radians(_rotation.x), glm::vec3(1,0,0));
    model = rotate(model, glm::radians(_rotation.y), glm::vec3(0,1,0));
    model = rotate(model, glm::radians(_rotation.z), glm::vec3(0,0,1));
    model = scale(model, glm::vec3(_scale));
    
    return model;
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

rei::transformation::Transform::operator std::string() const
{
    return "P: " + std::string(_position) + "; R: " + std::string(_rotation) + "; S: " + std::string(_scale);
}
