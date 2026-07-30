#pragma once

namespace rei::editor
{
    struct TransformationControlRectHandle
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;
        math::Vector2 Direction = {};
        math::Vector3 DragStartPosition = {};
        bool IsCorner = false;
        bool DragActive = false;
    };
}
