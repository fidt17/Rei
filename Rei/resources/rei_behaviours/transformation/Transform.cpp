#include "pch.h"
#include "Transform.h"

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
