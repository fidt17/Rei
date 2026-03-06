#pragma once

namespace rei::editor
{
    struct TransformationControlScaleArrow
    {
        ecs::Entity Entity = ecs::NULL_ENTITY;

        math::Vector3 Direction = {};

        bool DragActive;
        math::Vector3 TargetDragStartScale = {};
        f32 ArrowDragStartScale = {};
        math::Vector3 DragOffset = {};
        math::Plane DragPlane = {};
        f32 InitialScaleMlt = 0;
        f32 CurrentScaleMlt = 0;
    };
}
