#pragma once
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    namespace transformation_control_drag
    {
        constexpr f32 MOVE_SNAP_STEP = 0.25f;
        constexpr f32 ROTATION_SNAP_STEP_DEGREES = 10.0f;
        constexpr f32 SCALE_SNAP_STEP = 0.1f;

        bool IsSnappingEnabled();
        f32 SnapValue(f32 value, f32 step);
        math::Vector3 SnapScaleDelta(const math::Vector3& scaleDelta, const math::Vector3& direction, f32 step);
        bool HasRectTransformTargets(const std::shared_ptr<ecs::World>& world, const TransformationControl& control);
        void CaptureDragStartTargetStates(const std::shared_ptr<ecs::World>& world, TransformationControl& control);
        const TransformationControlTargetState* FindDragStartTargetState(const TransformationControl& control, ecs::Entity entity);
    }
}
