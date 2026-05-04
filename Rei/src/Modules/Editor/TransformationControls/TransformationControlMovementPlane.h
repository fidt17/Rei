#pragma once

namespace rei::editor
{
    struct TransformationControlMovementPlane
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;

        math::Vector3 FirstDirection = {};
        math::Vector3 SecondDirection = {};

        bool DragActive = false;
        math::Vector3 DragStartIntersection = {};
        math::Vector3 PartDragStartPosition = {};
        math::Plane DragPlane = {};
    };
}
