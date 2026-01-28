#pragma once
#include "glm/detail/type_quat.hpp"

namespace rei::editor
{
    struct TransformationControlRotationRing
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;

        math::Vector3 Direction = {};

        bool DragActive;
        glm::quat TargetDragStartRotation = {};
        math::Vector3 DragStartDirection = {};
        math::Vector3 DragAxis = {};
        math::Plane DragPlane = {};
    };
}
