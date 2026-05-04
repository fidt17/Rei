#include "pch.h"
#include "HandleTransformationControlsScaleDragSystem.h"

#include <cmath>

#include "TransformationControlDragUtilities.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    namespace drag = transformation_control_drag;

    HandleTransformationControlsScaleDragSystem::HandleTransformationControlsScaleDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void HandleTransformationControlsScaleDragSystem::OnUpdate()
    {
        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        if (control.Mode != Scale) return;

        HandleScaleDrag(control);
    }

    bool HandleTransformationControlsScaleDragSystem::HandleUiScaleDrag(TransformationControl& control) const
    {
        if (!drag::HasRectTransformTargets(_ecsWorld, control)) return false;

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
                drag::CaptureDragStartTargetStates(_ecsWorld, control);
            }

            if (arrow.DragActive)
            {
                const auto rawDelta = pointerPos - arrow.PartDragStartPosition;
                const f32 axisDelta = arrow.Direction.x != 0.0f ? rawDelta.x : -rawDelta.y;
                f32 scaleDeltaValue = axisDelta * 0.01f;
                if (drag::IsSnappingEnabled()) scaleDeltaValue = drag::SnapValue(scaleDeltaValue, drag::SCALE_SNAP_STEP);

                math::Vector3 scaleDelta = {};
                if (arrow.Direction.x != 0.0f) scaleDelta.x = scaleDeltaValue;
                if (arrow.Direction.y != 0.0f) scaleDelta.y = scaleDeltaValue;

                for (const auto entity : control.TargetEntities)
                {
                    if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform) || !HAS(entity, Transform)) continue;

                    const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
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

    void HandleTransformationControlsScaleDragSystem::HandleScaleDrag(TransformationControl& control) const
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

                drag::CaptureDragStartTargetStates(_ecsWorld, control);

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

                const auto appliedScaleDelta = drag::IsSnappingEnabled()
                    ? drag::SnapScaleDelta(scaleDelta, arrow.Direction, drag::SCALE_SNAP_STEP)
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

                        const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
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

                drag::CaptureDragStartTargetStates(_ecsWorld, control);
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
                    const f32 appliedScaleDelta = drag::IsSnappingEnabled()
                        ? drag::SnapValue(scaleMlt, drag::SCALE_SNAP_STEP)
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

                            const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
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
}
