#include "pch.h"
#include "HandleTransformationControlsDragSystem.h"

#include <algorithm>
#include <cmath>

#include "glm/gtc/quaternion.hpp"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "Common/Transform/RectTransformUtility.h"

namespace rei::editor
{
    namespace
    {
        constexpr f32 MOVE_SNAP_STEP = 0.25f;
        constexpr f32 ROTATION_SNAP_STEP_DEGREES = 10.0f;
        constexpr f32 SCALE_SNAP_STEP = 0.1f;
    }

    HandleTransformationControlsDragSystem::HandleTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    bool HandleTransformationControlsDragSystem::IsSnappingEnabled() const
    {
        return Input::IsKeyDown(GLFW_KEY_LEFT_CONTROL) || Input::IsKeyDown(GLFW_KEY_RIGHT_CONTROL);
    }

    f32 HandleTransformationControlsDragSystem::SnapValue(const f32 value, const f32 step) const
    {
        if (step <= 0.0f) return value;
        return std::round(value / step) * step;
    }

    math::Vector3 HandleTransformationControlsDragSystem::SnapScaleDelta(const math::Vector3& scaleDelta, const math::Vector3& direction, const f32 step) const
    {
        math::Vector3 snappedDelta = scaleDelta;

        if (direction.x != 0) snappedDelta.x = SnapValue(scaleDelta.x, step);
        if (direction.y != 0) snappedDelta.y = SnapValue(scaleDelta.y, step);
        if (direction.z != 0) snappedDelta.z = SnapValue(scaleDelta.z, step);

        return snappedDelta;
    }

    bool HandleTransformationControlsDragSystem::HasRectTransformTargets(const TransformationControl& control) const
    {
        for (const auto entity : control.TargetEntities)
        {
            if (!IS_DEAD(entity) && HAS(entity, ui::RectTransform)) return true;
        }

        return false;
    }

    void HandleTransformationControlsDragSystem::CaptureDragStartTargetStates(TransformationControl& control) const
    {
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

    const TransformationControlTargetState* HandleTransformationControlsDragSystem::FindDragStartTargetState(const TransformationControl& control, const ecs::Entity entity) const
    {
        for (const auto& state : control.DragStartTargetStates)
        {
            if (state.Entity == entity) return &state;
        }

        return nullptr;
    }

    void HandleTransformationControlsDragSystem::OnUpdate()
    {
        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);

        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT))
        {
            ResetDragState(control);
        }
        else
        {
            if (control.Mode == Movement)
            {
                HandleMovementDrag(control);
            }
            else if (control.Mode == Scale)
            {
                HandleScaleDrag(control);
            }
            else if (control.Mode == Rotation)
            {
                HandleRotationDrag(control);
            }
        }
    }

    void HandleTransformationControlsDragSystem::ResetDragState(TransformationControl& control) const
    {
        control.RightMovementArrow.DragActive = false;
        control.UpMovementArrow.DragActive = false;
        control.ForwardMovementArrow.DragActive = false;

        control.RightScaleArrow.DragActive = false;
        control.UpScaleArrow.DragActive = false;
        control.ForwardScaleArrow.DragActive = false;
        control.RootScale.DragActive = false;

        control.RightScaleArrow.CurrentScaleMlt = 0;
        control.UpScaleArrow.CurrentScaleMlt = 0;
        control.ForwardScaleArrow.CurrentScaleMlt = 0;
        control.RootScale.CurrentScaleMlt = 0;

        control.RightRotationRing.DragActive = false;
        control.UpRotationRing.DragActive = false;
        control.ForwardRotationRing.DragActive = false;

        control.RightRotationRing.DragStartDirection = {};
        control.UpRotationRing.DragStartDirection = {};
        control.ForwardRotationRing.DragStartDirection = {};

        control.RightRotationRing.DragAxis = {};
        control.UpRotationRing.DragAxis = {};
        control.ForwardRotationRing.DragAxis = {};

        control.DragStartTargetStates.clear();
    }

    bool HandleTransformationControlsDragSystem::HandleUiMovementDrag(TransformationControl& control) const
    {
        if (!HasRectTransformTargets(control)) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto tryMove = [&](TransformationControlMovementArrow& arrow) -> bool
        {
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);
            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.PartDragStartPosition = pointerPos;
                CaptureDragStartTargetStates(control);
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
                if (IsSnappingEnabled())
                {
                    delta.x = SnapValue(delta.x, MOVE_SNAP_STEP);
                    delta.y = SnapValue(delta.y, MOVE_SNAP_STEP);
                }

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform)) continue;

                    const auto* dragStartState = FindDragStartTargetState(control, entity);
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

    bool HandleTransformationControlsDragSystem::HandleUiScaleDrag(TransformationControl& control) const
    {
        if (!HasRectTransformTargets(control)) return false;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);
        auto tryScale = [&](TransformationControlScaleArrow& arrow) -> bool
        {
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);
            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.TargetDragStartScale = primaryTargetTransform.GetWorldScale();
                arrow.PartDragStartPosition = pointerPos;
                CaptureDragStartTargetStates(control);
            }

            if (arrow.DragActive)
            {
                const auto rawDelta = pointerPos - arrow.PartDragStartPosition;
                const f32 axisDelta = arrow.Direction.x != 0.0f ? rawDelta.x : -rawDelta.y;
                f32 scaleDeltaValue = axisDelta * 0.01f;
                if (IsSnappingEnabled()) scaleDeltaValue = SnapValue(scaleDeltaValue, SCALE_SNAP_STEP);

                math::Vector3 scaleDelta = {};
                if (arrow.Direction.x != 0.0f) scaleDelta.x = scaleDeltaValue;
                if (arrow.Direction.y != 0.0f) scaleDelta.y = scaleDeltaValue;

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform) || !HAS(entity, Transform)) continue;

                    const auto* dragStartState = FindDragStartTargetState(control, entity);
                    if (dragStartState == nullptr) continue;

                    GET(entity, Transform).GetLocalScale() = dragStartState->LocalScale + scaleDelta;
                }

                arrow.CurrentScaleMlt = scaleDeltaValue;
            }

            return arrow.DragActive;
        };

        if (control.RightScaleArrow.DragActive && tryScale(control.RightScaleArrow)) return true;
        if (control.UpScaleArrow.DragActive && tryScale(control.UpScaleArrow)) return true;

        if (tryScale(control.RightScaleArrow)) return true;
        if (tryScale(control.UpScaleArrow)) return true;
        return false;
    }

    bool HandleTransformationControlsDragSystem::HandleUiRotationDrag(TransformationControl& control) const
    {
        if (!HasRectTransformTargets(control)) return false;

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
                CaptureDragStartTargetStates(control);
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
                const f32 appliedAngleDeg = IsSnappingEnabled()
                    ? SnapValue(angleDeg, ROTATION_SNAP_STEP_DEGREES)
                    : angleDeg;
                const auto delta = glm::angleAxis(glm::radians(-appliedAngleDeg), glm::vec3(0, 0, 1));

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform) || !HAS(entity, Transform)) continue;

                    const auto* dragStartState = FindDragStartTargetState(control, entity);
                    if (dragStartState == nullptr) continue;

                    GET(entity, Transform).SetRotation(glm::normalize(delta * dragStartState->LocalRotation));
                }
            }

            return ring.DragActive;
        };

        if (control.ForwardRotationRing.DragActive && tryRotate(control.ForwardRotationRing)) return true;
        return tryRotate(control.ForwardRotationRing);
    }

    void HandleTransformationControlsDragSystem::HandleMovementDrag(TransformationControl& control) const
    {
        if (HandleUiMovementDrag(control)) return;
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

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.PartDragStartPosition = control.PivotWorldPosition;
                arrow.DragPlane = math::Plane(arrowRight, pointerListener.CollisionPoint);

                CaptureDragStartTargetStates(control);

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
                const f32 appliedDeltaOnAxis = IsSnappingEnabled()
                    ? SnapValue(deltaOnAxis, MOVE_SNAP_STEP)
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

                        const auto* dragStartState = FindDragStartTargetState(control, entity);
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

    void HandleTransformationControlsDragSystem::HandleScaleDrag(TransformationControl& control) const
    {
        if (HandleUiScaleDrag(control)) return;
        if (!control.HasTargets()) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        i32 screenWidth = 1;
        i32 screenHeight = 1;
        mainCamera.Get().GetOutputSize(screenWidth, screenHeight);

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);

        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);

        auto tryScale = [&](TransformationControlScaleArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto arrowPos = arrowTransform.GetWorldPosition();
            const auto arrowRotation = arrowTransform.GetWorldRotation();
            const auto arrowForward = math::Vector3(arrowRotation * glm::vec3(0, 0, 1));
            const auto arrowRight = math::Vector3(arrowRotation * glm::vec3(1, 0, 0));
            const auto arrowScale = arrowTransform.GetWorldScale().x;

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.TargetDragStartScale = primaryTargetTransform.GetWorldScale();
                arrow.ArrowDragStartScale = arrowScale;
                arrow.DragPlane = math::Plane(arrowRight, pointerListener.CollisionPoint);

                CaptureDragStartTargetStates(control);

                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                arrow.DragOffset = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward) / arrow.ArrowDragStartScale;
                arrow.CurrentScaleMlt = 0;
            }

            if (arrow.DragActive)
            {
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);
                const auto projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward);

                const f32 scaleSign = static_cast<f32>(math::Sign(math::Vector3::Dot(arrowForward, projectionOnArrowDirection)));
                const f32 scaleMlt = scaleSign * (projectionOnArrowDirection.Length() / arrow.ArrowDragStartScale) / arrow.DragOffset.Length();

                math::Vector3 scaleDelta = {};
                scaleDelta.x = std::abs(arrow.TargetDragStartScale.x) * (arrow.Direction.x != 0 ? (arrow.Direction.x * (scaleMlt - 1)) : 0);
                scaleDelta.y = std::abs(arrow.TargetDragStartScale.y) * (arrow.Direction.y != 0 ? (arrow.Direction.y * (scaleMlt - 1)) : 0);
                scaleDelta.z = std::abs(arrow.TargetDragStartScale.z) * (arrow.Direction.z != 0 ? (arrow.Direction.z * (scaleMlt - 1)) : 0);

                const auto appliedScaleDelta = IsSnappingEnabled()
                    ? SnapScaleDelta(scaleDelta, arrow.Direction, SCALE_SNAP_STEP)
                    : scaleDelta;

                if (control.TargetEntities.size() == 1)
                {
                    primaryTargetTransform.SetWorldScale(arrow.TargetDragStartScale + appliedScaleDelta);
                }
                else
                {
                    for (const auto entity : control.TargetEntities)
                    {
                        if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

                        const auto* dragStartState = FindDragStartTargetState(control, entity);
                        if (dragStartState == nullptr) continue;

                        GET(entity, Transform).GetLocalScale() = dragStartState->LocalScale + appliedScaleDelta;
                    }
                }

                arrow.CurrentScaleMlt = scaleMlt - 1;
            }

            return arrow.DragActive;
        };

        auto tryScaleRoot = [&](TransformationControlScaleArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto arrowPos = arrowTransform.GetWorldPosition();
            const auto arrowScale = arrowTransform.GetWorldScale().x;

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.TargetDragStartScale = primaryTargetTransform.GetWorldScale();
                arrow.ArrowDragStartScale = arrowScale;
                const auto cameraForward = math::Vector3(mainCamera.Get().GetTransform().GetWorldRotation() * glm::vec3(0, 0, 1));
                arrow.DragPlane = math::Plane(cameraForward, arrowPos);
                arrow.InitialScaleMlt = 0;
                arrow.CurrentScaleMlt = 0;

                CaptureDragStartTargetStates(control);
            }

            if (arrow.DragActive)
            {
                const auto screenDiagonalVector = math::Vector3::Normalize({static_cast<f32>(screenWidth), static_cast<f32>(screenHeight), 0});
                const auto screenOtherDiagonalVector = math::Vector3(screenDiagonalVector.y, -screenDiagonalVector.x, 0);
                const f32 screenDiagonalLength = math::Vector3::Length({static_cast<f32>(screenWidth), static_cast<f32>(screenHeight), 0});

                const auto arrowScreenPos = mainCamera.Get().WorldToScreenPosition(arrowPos);
                const f32 offsetProjection = math::Vector3::Dot(arrowScreenPos, screenDiagonalVector);
                const auto offset = arrowScreenPos - (screenDiagonalVector * offsetProjection);
                const f32 pointProjection = math::Vector3::Dot(pointerPos, screenDiagonalVector);
                const auto newProjection = (screenDiagonalVector * pointProjection) + offset;

                const auto scaleDir = pointerPos - newProjection;
                const f32 scaleSign = static_cast<f32>(math::Sign(math::Vector3::Dot(screenOtherDiagonalVector, scaleDir)));

                f32 scaleMlt = (scaleDir.Length() / screenDiagonalLength * scaleSign * 10) * arrow.ArrowDragStartScale;

                if (arrow.InitialScaleMlt == 0)
                {
                    arrow.InitialScaleMlt = scaleMlt;
                }
                else
                {
                    scaleMlt -= arrow.InitialScaleMlt;
                    const f32 appliedScaleDelta = IsSnappingEnabled()
                        ? SnapValue(scaleMlt, SCALE_SNAP_STEP)
                        : scaleMlt;
                    const auto scaleDelta = math::Vector3(appliedScaleDelta, appliedScaleDelta, appliedScaleDelta);

                    if (control.TargetEntities.size() == 1)
                    {
                        primaryTargetTransform.SetWorldScale(arrow.TargetDragStartScale + scaleDelta);
                    }
                    else
                    {
                        for (const auto entity : control.TargetEntities)
                        {
                            if (IS_DEAD(entity) || !HAS(entity, Transform)) continue;

                            const auto* dragStartState = FindDragStartTargetState(control, entity);
                            if (dragStartState == nullptr) continue;

                            GET(entity, Transform).GetLocalScale() = dragStartState->LocalScale + scaleDelta;
                        }
                    }
                }

                arrow.CurrentScaleMlt = scaleMlt;
            }

            return arrow.DragActive;
        };

        if (control.RootScale.DragActive && tryScaleRoot(control.RootScale)) return;
        if (control.RightScaleArrow.DragActive && tryScale(control.RightScaleArrow)) return;
        if (control.UpScaleArrow.DragActive && tryScale(control.UpScaleArrow)) return;
        if (control.ForwardScaleArrow.DragActive && tryScale(control.ForwardScaleArrow)) return;

        if (tryScaleRoot(control.RootScale)) return;
        if (tryScale(control.ForwardScaleArrow)) return;
        if (tryScale(control.RightScaleArrow)) return;
        if (tryScale(control.UpScaleArrow)) return;
    }

    void HandleTransformationControlsDragSystem::HandleRotationDrag(TransformationControl& control) const
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

                CaptureDragStartTargetStates(control);

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
                const f32 appliedAngleDeg = IsSnappingEnabled()
                    ? SnapValue(angleDeg, ROTATION_SNAP_STEP_DEGREES)
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

                        const auto* dragStartState = FindDragStartTargetState(control, entity);
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
