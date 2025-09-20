#pragma once

namespace rei::editor
{
    struct TransformationControlMovementArrow
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;

        math::Vector3 Direction = {};
        
        bool DragActive;
        math::Vector3 DragStartPosition = {};
        math::Vector3 DragOffset = {};
        math::Plane DragPlane = {};
    };
}
