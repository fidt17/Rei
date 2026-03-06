#include "pch.h"
#include "HandleTransformationControlsDragSystem.h"

#include "Modules/Input/Input.h"
#include <cmath>
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    HandleTransformationControlsDragSystem::HandleTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld): System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
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
    }

    void HandleTransformationControlsDragSystem::HandleMovementDrag(TransformationControl& control) const
    {
        if (IS_DEAD(control.TargetEntity)) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto tryMove = [&](TransformationControlMovementArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto& arrowPos = arrowTransform.GetPosition();
            const auto arrowForward = arrowTransform.GetForward();

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.PartDragStartPosition = arrowPos;

                arrow.DragPlane = math::Plane(arrowTransform.GetRight(), pointerListener.CollisionPoint);

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward);

                const auto arrowScale = arrowTransform.GetScale().x;
                arrow.DragOffset = projectionOnArrowDirection / arrowScale;
            }

            if (arrow.DragActive)
            {
                auto& targetTransform = GET(control.TargetEntity, Transform);
                const auto offsetScaled = arrow.DragOffset * arrowTransform.GetScale();

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrow.PartDragStartPosition, arrowForward);

                targetTransform.GetPosition() = arrow.PartDragStartPosition + projectionOnArrowDirection - offsetScaled;
            }

            return arrow.DragActive;
        };

        // allow movement only along 1 arrow at a time
        if (control.RightMovementArrow.DragActive && tryMove(control.RightMovementArrow)) return;
        if (control.UpMovementArrow.DragActive && tryMove(control.UpMovementArrow)) return;
        if (control.ForwardMovementArrow.DragActive && tryMove(control.ForwardMovementArrow)) return;

        if (tryMove(control.RightMovementArrow)) return;
        if (tryMove(control.UpMovementArrow)) return;
        if (tryMove(control.ForwardMovementArrow)) return;
    }

    void HandleTransformationControlsDragSystem::HandleScaleDrag(TransformationControl& control) const
    {
        if (IS_DEAD(control.TargetEntity)) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        i32 screenWidth = 1, screenHeight = 1;
        mainCamera.Get().GetOutputSize(screenWidth, screenHeight);

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);

        auto& targetTransform = GET(control.TargetEntity, Transform);
        auto& targetScale = targetTransform.GetScale();

        // for regular scale arrows
        auto tryScale = [&](TransformationControlScaleArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto& arrowPos = arrowTransform.GetPosition();
            const auto arrowForward = arrowTransform.GetForward();
            const auto arrowScale = arrowTransform.GetScale().x;

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.TargetDragStartScale = targetTransform.GetScale();
                arrow.ArrowDragStartScale = arrowScale;
                arrow.DragPlane = math::Plane(arrowTransform.GetRight(), pointerListener.CollisionPoint);

                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                arrow.DragOffset = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward) /  arrow.ArrowDragStartScale;
                arrow.CurrentScaleMlt = 0;
            }

            if (arrow.DragActive)
            {
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);
                const auto projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward);

                const f32 scaleSign = static_cast<f32>(math::Sign(math::Vector3::Dot(arrowForward, projectionOnArrowDirection)));
                const f32 scaleMlt = scaleSign * (projectionOnArrowDirection.Length() / arrow.ArrowDragStartScale) / (arrow.DragOffset.Length());

                targetScale.x = arrow.TargetDragStartScale.x + std::abs(arrow.TargetDragStartScale.x) * (arrow.Direction.x != 0 ? (arrow.Direction.x * (scaleMlt - 1)) : 0);
                targetScale.y = arrow.TargetDragStartScale.y + std::abs(arrow.TargetDragStartScale.y) * (arrow.Direction.y != 0 ? (arrow.Direction.y * (scaleMlt - 1)) : 0);
                targetScale.z = arrow.TargetDragStartScale.z + std::abs(arrow.TargetDragStartScale.z) * (arrow.Direction.z != 0 ? (arrow.Direction.z * (scaleMlt - 1)) : 0);

                arrow.CurrentScaleMlt = scaleMlt - 1;
            }

            return arrow.DragActive;
        };

        // for root scale element (in all directions)
        auto tryScaleRoot = [&](TransformationControlScaleArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto& arrowPos = arrowTransform.GetPosition();
            const auto arrowScale = arrowTransform.GetScale().x;

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.TargetDragStartScale = targetTransform.GetScale();
                arrow.ArrowDragStartScale = arrowScale;
                arrow.DragPlane = math::Plane(mainCamera.Get().GetTransform().GetForward(), arrowPos);
                arrow.InitialScaleMlt = 0;
                arrow.CurrentScaleMlt = 0;
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

                f32 scaleMlt = (scaleDir.Length() / screenDiagonalLength * scaleSign * 10) * (arrow.ArrowDragStartScale);

                if (arrow.InitialScaleMlt == 0)
                {
                    arrow.InitialScaleMlt = scaleMlt;
                }
                else
                {
                    scaleMlt -= arrow.InitialScaleMlt;
                    targetScale = arrow.TargetDragStartScale + math::Vector3(scaleMlt, scaleMlt, scaleMlt);
                }

                arrow.CurrentScaleMlt = scaleMlt;
            }

            return arrow.DragActive;
        };

        // allow scale only along 1 direction at a time
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
        if (IS_DEAD(control.TargetEntity)) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);

        auto& targetTransform = GET(control.TargetEntity, Transform);
        const auto targetPosition = targetTransform.GetPosition();

        auto tryRotate = [&](TransformationControlRotationRing& ring) -> bool
        {
            auto& ringTransform = GET(ring.Entity, Transform);
            const auto& pointerListener = GET(ring.Entity, physics::PointerCollisionListener);

            if (!ring.DragActive && pointerListener.IsInside)
            {
                ring.DragAxis = math::Vector3::Normalize(ringTransform.GetForward());
                ring.DragPlane = math::Plane(ring.DragAxis, targetPosition);
                ring.DragStartDirection = {};

                math::Vector3 planeIntersectionPoint;
                if (PlaneRayIntersection(ring.DragPlane, screenPointRay, planeIntersectionPoint))
                {
                    const auto startDir = planeIntersectionPoint - targetPosition;
                    if (startDir.Length() > 0.0001f)
                    {
                        ring.DragActive = true;
                        ring.TargetDragStartRotation = targetTransform.GetRotation();
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
                const f32 angleRad = std::atan2(signedArea, dotValue);
                const f32 angleDeg = angleRad * (180.0f / PI);

                targetTransform.SetRotation(ring.TargetDragStartRotation);
                targetTransform.RotateWorld(angleDeg, ring.DragAxis);
            }

            return ring.DragActive;
        };

        // allow rotation only around 1 ring at a time
        if (control.RightRotationRing.DragActive && tryRotate(control.RightRotationRing)) return;
        if (control.UpRotationRing.DragActive && tryRotate(control.UpRotationRing)) return;
        if (control.ForwardRotationRing.DragActive && tryRotate(control.ForwardRotationRing)) return;

        if (tryRotate(control.RightRotationRing)) return;
        if (tryRotate(control.UpRotationRing)) return;
        if (tryRotate(control.ForwardRotationRing)) return;
    }
}
