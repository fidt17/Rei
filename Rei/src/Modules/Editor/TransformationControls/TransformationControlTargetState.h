#pragma once

#include "glm/detail/type_quat.hpp"

namespace rei::editor
{
    struct TransformationControlTargetState
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;
        math::Vector3 LocalPosition = {};
        math::Vector3 LocalScale = math::Vector3(1, 1, 1);
        glm::quat LocalRotation = glm::quat(1, 0, 0, 0);
        math::Vector2 AnchoredPosition = {};
        math::Vector2 SizeDelta = {};
    };
}
