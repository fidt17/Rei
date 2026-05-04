#include "pch.h"
#include "HandleTransformationControlsRotationDragSystem.h"

#include <algorithm>
#include <cmath>

#include "glm/gtc/quaternion.hpp"
#include "TransformationControlDragUtilities.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    namespace drag = transformation_control_drag;

    HandleTransformationControlsRotationDragSystem::HandleTransformationControlsRotationDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void HandleTransformationControlsRotationDragSystem::OnUpdate()
    {
        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        if (control.Mode != Rotation) return;

        HandleRotationDrag(control);
    }

    bool HandleTransformationControlsRotationDragSystem::HandleUiRotationDrag(TransformationControl& control) const
    {
        if (!drag::HasRectTransformTargets(_ecsWorld, control)) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);
        auto tryRotate = [&](TransformationControlRotationRing& ring) -> bool
        {
            const auto& pointerListener = GET(ring.Entity, physics::PointerCollisionListener);
            const math::Vector2 center = control.PivotScreenPosition;
            const math::Vector2 pointer(pointerPos.x, pointerPos.y);

            if (!ring.DragActive && pointerListener.IsInside)
            {
                ring.DragActive = true;
                ring.TargetDragStartRotation = primaryTargetTransform.GetWorldRotation();
                ring.DragStartDirection = math::Vector3(pointer.x - center.x, pointer.y - center.y, 0.0f);
                drag::CaptureDragStartTargetStates(_ecsWorld, control);
            }

            if (ring.DragActive)
            {
                auto start = ring.DragStartDirection;
                auto current = math::Vector3(pointer.x - center.x, pointer.y - center.y, 0.0f);
                if (start.Length() <= 0.0001f || current.Length() <= 0.0001f) return true;

                start = math::Vector3::Normalize(start);
                current = math::Vector3::Normalize(current);
                f32 dotValue = std::clamp(math::Vector3::Dot(start, current), -1.0f, 1.0f);
                const f32 signedArea = start.x * current.y - start.y * current.x;
                const f32 angleDeg = static_cast<f32>(std::atan2(signedArea, dotValue) * (180.0f / PI));
                const f32 appliedAngleDeg = drag::IsSnappingEnabled()
                    ? drag::SnapValue(angleDeg, drag::ROTATION_SNAP_STEP_DEGREES)
                    : angleDeg;
                const auto delta = glm::angleAxis(glm::radians(-appliedAngleDeg), glm::vec3(0, 0, 1));

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform) || !HAS(entity, Transform)) continue;

                    const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
                    if (dragStartState == nullptr) continue;

                    GET(entity, Transform).SetRotation(glm::normalize(delta * dragStartState->LocalRotation));
                }
            }

            return ring.DragActive;
        };

        if (control.ForwardRotationRing.DragActive && tryRotate(control.ForwardRotationRing)) return true;
        return tryRotate(control.ForwardRotationRing);
    }

    void HandleTransformationControlsRotationDragSystem::HandleRotationDrag(TransformationControl& control) const
    {
        if (HandleUiRotationDrag(control)) return;
        if (!control.HasTargets()) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);

        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);
        const auto targetPosition = control.PivotWorldPosition;

        auto tryRotate = [&](TransformationControlRotationRing& ring) -> bool
        {
            auto& ringTransform = GET(ring.Entity, Transform);
            const auto& pointerListener = GET(ring.Entity, physics::PointerCollisionListener);

            if (!ring.DragActive && pointerListener.IsInside)
            {
                ring.DragAxis = math::Vector3::Normalize(math::Vector3(ringTransform.GetWorldRotation() * glm::vec3(0, 0, 1)));
                ring.DragPlane = math::Plane(ring.DragAxis, targetPosition);
                ring.DragStartDirection = {};

                drag::CaptureDragStartTargetStates(_ecsWorld, control);

                math::Vector3 planeIntersectionPoint;
                if (PlaneRayIntersection(ring.DragPlane, screenPointRay, planeIntersectionPoint))
                {
                    const auto startDir = planeIntersectionPoint - targetPosition;
                    if (startDir.Length() > 0.0001f)
                    {
                        ring.DragActive = true;
                        ring.TargetDragStartRotation = primaryTargetTransform.GetWorldRotation();
                        ring.DragStartDirection = math::Vector3::Normalize(startDir);
                    }
                }
            }

            if (ring.DragActive)
            {
                math::Vector3 planeIntersectionPoint;
                if (!PlaneRayIntersection(ring.DragPlane, screenPointRay, planeIntersectionPoint)) return ring.DragActive;

                auto currentDir = planeIntersectionPoint - targetPosition;
                if (currentDir.Length() <= 0.0001f || ring.DragStartDirection.Length() <= 0.0001f) return ring.DragActive;

                currentDir = math::Vector3::Normalize(currentDir);

                f32 dotValue = math::Vector3::Dot(ring.DragStartDirection, currentDir);
                if (dotValue > 1.0f) dotValue = 1.0f;
                if (dotValue < -1.0f) dotValue = -1.0f;

                const f32 signedArea = math::Vector3::Dot(ring.DragAxis, math::Vector3::Cross(ring.DragStartDirection, currentDir));
                const f32 angleRad = static_cast<f32>(std::atan2(signedArea, dotValue));
                const f32 angleDeg = static_cast<f32>(angleRad * (180.0f / PI));
                const f32 appliedAngleDeg = drag::IsSnappingEnabled()
                    ? drag::SnapValue(angleDeg, drag::ROTATION_SNAP_STEP_DEGREES)
                    : angleDeg;

                const auto delta = glm::angleAxis(glm::radians(appliedAngleDeg), glm::vec3(ring.DragAxis));

                if (control.TargetEntities.size() == 1)
                {
                    primaryTargetTransform.SetWorldRotation(glm::normalize(delta * ring.TargetDragStartRotation));
                }
                else
                {
                    for (const auto entity : control.TargetEntities)
                    {
                        if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

                        const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
                        if (dragStartState == nullptr) continue;

                        GET(entity, Transform).SetRotation(glm::normalize(delta * dragStartState->LocalRotation));
                    }
                }
            }

            return ring.DragActive;
        };

        if (control.RightRotationRing.DragActive && tryRotate(control.RightRotationRing)) return;
        if (control.UpRotationRing.DragActive && tryRotate(control.UpRotationRing)) return;
        if (control.ForwardRotationRing.DragActive && tryRotate(control.ForwardRotationRing)) return;

        if (tryRotate(control.RightRotationRing)) return;
        if (tryRotate(control.UpRotationRing)) return;
        if (tryRotate(control.ForwardRotationRing)) return;
    }
}
