#include "pch.h"
#include "TransformationControlDragUtilities.h"

#include <cmath>

#include "Modules/Input/Input.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor::transformation_control_drag
{
    bool IsSnappingEnabled()
    {
        return Input::IsKeyDown(GLFW_KEY_LEFT_CONTROL) || Input::IsKeyDown(GLFW_KEY_RIGHT_CONTROL);
    }

    f32 SnapValue(const f32 value, const f32 step)
    {
        if (step <= 0.0f) return value;
        return std::round(value / step) * step;
    }

    math::Vector3 SnapScaleDelta(const math::Vector3& scaleDelta, const math::Vector3& direction, const f32 step)
    {
        math::Vector3 snappedDelta = scaleDelta;

        if (direction.x != 0) snappedDelta.x = SnapValue(scaleDelta.x, step);
        if (direction.y != 0) snappedDelta.y = SnapValue(scaleDelta.y, step);
        if (direction.z != 0) snappedDelta.z = SnapValue(scaleDelta.z, step);

        return snappedDelta;
    }

    bool HasRectTransformTargets(const std::shared_ptr<ecs::World>& world, const TransformationControl& control)
    {
        ECS_WORLD(world);

        for (const auto entity : control.TargetEntities)
        {
            if (!IS_DEAD(entity) && HAS(entity, ui::RectTransform)) return true;
        }

        return false;
    }

    void CaptureDragStartTargetStates(const std::shared_ptr<ecs::World>& world, TransformationControl& control)
    {
        ECS_WORLD(world);

        control.DragStartTargetStates.clear();

        for (const auto entity : control.TargetEntities)
        {
            if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

            const auto& transform = GET(entity, Transform);
            TransformationControlTargetState targetState = {};
            targetState.Entity = entity;
            targetState.LocalPosition = transform.GetLocalPosition();
            targetState.LocalScale = transform.GetLocalScale();
            targetState.LocalRotation = transform.GetLocalRotation();
            if (HAS(entity, ui::RectTransform))
            {
                targetState.AnchoredPosition = GET(entity, ui::RectTransform).GetAnchoredPosition();
            }
            control.DragStartTargetStates.push_back(targetState);
        }
    }

    const TransformationControlTargetState* FindDragStartTargetState(const TransformationControl& control, const ecs::Entity entity)
    {
        for (const auto& state : control.DragStartTargetStates)
        {
            if (state.Entity == entity) return &state;
        }

        return nullptr;
    }
}
