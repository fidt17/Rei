#include "pch.h"
#include "HandleTransformationControlsMovementDragSystem.h"

#include "TransformationControlDragUtilities.h"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    namespace drag = transformation_control_drag;

    HandleTransformationControlsMovementDragSystem::HandleTransformationControlsMovementDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void HandleTransformationControlsMovementDragSystem::OnUpdate()
    {
        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        if (control.Mode != Movement) return;

        HandleMovementDrag(control);
    }

    bool HandleTransformationControlsMovementDragSystem::HandleUiMovementPlaneDrag(TransformationControl& control) const
    {
        if (!drag::HasRectTransformTargets(_ecsWorld, control)) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto& plane = control.RightUpMovementPlane;
        const auto& pointerListener = GET(plane.Entity, physics::PointerCollisionListener);
        if (!plane.DragActive && drag::ShouldStartPointerDrag(pointerListener))
        {
            plane.DragActive = true;
            plane.DragStartIntersection = pointerPos;
            EditorPointerInteractionState::Consume();
            drag::CaptureDragStartTargetStates(_ecsWorld, control);
        }

        if (!plane.DragActive) return false;

        const auto canvasEntity = ui_utility::FindCanvasEntity(control.PrimaryTargetEntity);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return true;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return true;

        i32 width = 1;
        i32 height = 1;
        mainCamera.Get().GetOutputSize(width, height);

        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(GET(canvasEntity, ui::Canvas), width, height);
        if (scaleFactor <= 0.0f) return true;

        math::Vector2 delta(
            (pointerPos.x - plane.DragStartIntersection.x) / scaleFactor,
            -(pointerPos.y - plane.DragStartIntersection.y) / scaleFactor);
        if (drag::IsSnappingEnabled())
        {
            delta.x = drag::SnapValue(delta.x, drag::MOVE_SNAP_STEP);
            delta.y = drag::SnapValue(delta.y, drag::MOVE_SNAP_STEP);
        }

        for (const auto entity : control.TargetEntities)
        {
            if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform)) continue;

            const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
            if (dragStartState == nullptr) continue;

            GET(entity, ui::RectTransform).GetAnchoredPosition() = dragStartState->AnchoredPosition + delta;
        }

        return true;
    }

    bool HandleTransformationControlsMovementDragSystem::HandleUiMovementDrag(TransformationControl& control) const
    {
        if (!drag::HasRectTransformTargets(_ecsWorld, control)) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto tryMove = [&](TransformationControlMovementArrow& arrow) -> bool
        {
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);
            if (!arrow.DragActive && drag::ShouldStartPointerDrag(pointerListener))
            {
                arrow.DragActive = true;
                arrow.PartDragStartPosition = pointerPos;
                EditorPointerInteractionState::Consume();
                drag::CaptureDragStartTargetStates(_ecsWorld, control);
            }

            if (arrow.DragActive)
            {
                const auto canvasEntity = ui_utility::FindCanvasEntity(control.PrimaryTargetEntity);
                if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return true;

                const auto mainCamera = render::Camera::GetMainCamera();
                if (mainCamera.IsNull()) return true;

                i32 width = 1;
                i32 height = 1;
                mainCamera.Get().GetOutputSize(width, height);

                const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(GET(canvasEntity, ui::Canvas), width, height);
                if (scaleFactor <= 0.0f) return true;

                math::Vector2 delta(
                    (pointerPos.x - arrow.PartDragStartPosition.x) / scaleFactor,
                    -(pointerPos.y - arrow.PartDragStartPosition.y) / scaleFactor);

                if (arrow.Direction.x == 0.0f) delta.x = 0.0f;
                if (arrow.Direction.y == 0.0f) delta.y = 0.0f;
                if (drag::IsSnappingEnabled())
                {
                    delta.x = drag::SnapValue(delta.x, drag::MOVE_SNAP_STEP);
                    delta.y = drag::SnapValue(delta.y, drag::MOVE_SNAP_STEP);
                }

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform)) continue;

                    const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
                    if (dragStartState == nullptr) continue;

                    GET(entity, ui::RectTransform).GetAnchoredPosition() = dragStartState->AnchoredPosition + delta;
                }
            }

            return arrow.DragActive;
        };

        if (control.RightMovementArrow.DragActive && tryMove(control.RightMovementArrow)) return true;
        if (control.UpMovementArrow.DragActive && tryMove(control.UpMovementArrow)) return true;

        if (tryMove(control.RightMovementArrow)) return true;
        if (tryMove(control.UpMovementArrow)) return true;
        return false;
    }

    bool HandleTransformationControlsMovementDragSystem::HandleMovementPlaneDrag(TransformationControl& control) const
    {
        if (!control.HasTargets()) return false;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);

        auto tryMove = [&](TransformationControlMovementPlane& plane) -> bool
        {
            auto& planeTransform = GET(plane.Entity, Transform);
            const auto& pointerListener = GET(plane.Entity, physics::PointerCollisionListener);

            if (!plane.DragActive && drag::ShouldStartPointerDrag(pointerListener))
            {
                const auto planeRotation = planeTransform.GetWorldRotation();
                const auto planeNormal = math::Vector3(planeRotation * glm::vec3(0, 0, 1));
                plane.DragPlane = math::Plane(planeNormal, pointerListener.CollisionPoint);

                math::Vector3 planeIntersectionPoint;
                if (PlaneRayIntersection(plane.DragPlane, screenPointRay, planeIntersectionPoint))
                {
                    plane.DragActive = true;
                    plane.DragStartIntersection = planeIntersectionPoint;
                    plane.PartDragStartPosition = control.PivotWorldPosition;
                    EditorPointerInteractionState::Consume();
                    drag::CaptureDragStartTargetStates(_ecsWorld, control);
                }
            }

            if (!plane.DragActive) return false;

            math::Vector3 planeIntersectionPoint;
            if (!PlaneRayIntersection(plane.DragPlane, screenPointRay, planeIntersectionPoint)) return true;

            auto firstAxis = plane.FirstDirection;
            auto secondAxis = plane.SecondDirection;
            if (!control.IsUsingWorldSpace())
            {
                const auto targetRotation = primaryTargetTransform.GetWorldRotation();
                firstAxis = firstAxis.Rotate(targetRotation);
                secondAxis = secondAxis.Rotate(targetRotation);
            }

            const auto rawDelta = planeIntersectionPoint - plane.DragStartIntersection;
            f32 firstDelta = math::Vector3::Dot(rawDelta, math::Vector3::Normalize(firstAxis));
            f32 secondDelta = math::Vector3::Dot(rawDelta, math::Vector3::Normalize(secondAxis));
            if (drag::IsSnappingEnabled())
            {
                firstDelta = drag::SnapValue(firstDelta, drag::MOVE_SNAP_STEP);
                secondDelta = drag::SnapValue(secondDelta, drag::MOVE_SNAP_STEP);
            }

            const auto movementDelta = math::Vector3::Normalize(firstAxis) * firstDelta + math::Vector3::Normalize(secondAxis) * secondDelta;
            if (control.TargetEntities.size() == 1)
            {
                primaryTargetTransform.SetWorldPosition(plane.PartDragStartPosition + movementDelta);
                return true;
            }

            for (const auto entity : control.TargetEntities)
            {
                if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

                const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
                if (dragStartState == nullptr) continue;

                GET(entity, Transform).GetLocalPosition() = dragStartState->LocalPosition + movementDelta;
            }

            return true;
        };

        if (control.RightUpMovementPlane.DragActive && tryMove(control.RightUpMovementPlane)) return true;
        if (control.RightForwardMovementPlane.DragActive && tryMove(control.RightForwardMovementPlane)) return true;
        if (control.UpForwardMovementPlane.DragActive && tryMove(control.UpForwardMovementPlane)) return true;

        if (tryMove(control.RightUpMovementPlane)) return true;
        if (tryMove(control.RightForwardMovementPlane)) return true;
        if (tryMove(control.UpForwardMovementPlane)) return true;
        return false;
    }

    void HandleTransformationControlsMovementDragSystem::HandleMovementDrag(TransformationControl& control) const
    {
        if (HandleUiMovementPlaneDrag(control)) return;
        if (HandleUiMovementDrag(control)) return;
        if (HandleMovementPlaneDrag(control)) return;
        if (!control.HasTargets()) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto tryMove = [&](TransformationControlMovementArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto arrowPos = arrowTransform.GetWorldPosition();
            const auto arrowRotation = arrowTransform.GetWorldRotation();
            const auto arrowForward = math::Vector3(arrowRotation * glm::vec3(0, 0, 1));
            const auto arrowRight = math::Vector3(arrowRotation * glm::vec3(1, 0, 0));

            if (!arrow.DragActive && drag::ShouldStartPointerDrag(pointerListener))
            {
                arrow.DragActive = true;
                arrow.PartDragStartPosition = control.PivotWorldPosition;
                arrow.DragPlane = math::Plane(arrowRight, pointerListener.CollisionPoint);

                EditorPointerInteractionState::Consume();
                drag::CaptureDragStartTargetStates(_ecsWorld, control);

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward);
                const auto arrowScale = arrowTransform.GetLocalScale().x;
                arrow.DragOffset = projectionOnArrowDirection / arrowScale;
            }

            if (arrow.DragActive)
            {
                auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);
                const auto offsetScaled = arrow.DragOffset * arrowTransform.GetWorldScale();

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrow.PartDragStartPosition, arrowForward);
                const auto axis = math::Vector3::Normalize(arrowForward);
                const auto rawDelta = projectionOnArrowDirection - offsetScaled;
                const f32 deltaOnAxis = math::Vector3::Dot(rawDelta, axis);
                const f32 appliedDeltaOnAxis = drag::IsSnappingEnabled()
                    ? drag::SnapValue(deltaOnAxis, drag::MOVE_SNAP_STEP)
                    : deltaOnAxis;
                const auto movementDelta = axis * appliedDeltaOnAxis;

                if (control.TargetEntities.size() == 1)
                {
                    primaryTargetTransform.SetWorldPosition(arrow.PartDragStartPosition + movementDelta);
                }
                else
                {
                    for (const auto entity : control.TargetEntities)
                    {
                        if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

                        const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
                        if (dragStartState == nullptr) continue;

                        GET(entity, Transform).GetLocalPosition() = dragStartState->LocalPosition + movementDelta;
                    }
                }
            }

            return arrow.DragActive;
        };

        if (control.RightMovementArrow.DragActive && tryMove(control.RightMovementArrow)) return;
        if (control.UpMovementArrow.DragActive && tryMove(control.UpMovementArrow)) return;
        if (control.ForwardMovementArrow.DragActive && tryMove(control.ForwardMovementArrow)) return;

        if (tryMove(control.RightMovementArrow)) return;
        if (tryMove(control.UpMovementArrow)) return;
        if (tryMove(control.ForwardMovementArrow)) return;
    }
}
