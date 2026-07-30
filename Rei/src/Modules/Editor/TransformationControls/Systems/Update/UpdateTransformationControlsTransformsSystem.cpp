#include "pch.h"
#include "UpdateTransformationControlsTransformsSystem.h"

#include <algorithm>
#include <array>

#include "glm/gtc/quaternion.hpp"
#include "Common/Math/math.h"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    namespace
    {
        math::Vector2 TransformUiPixelPoint(const glm::mat4& model, const math::Vector2& point)
        {
            const auto transformed = model * glm::vec4(point.x, point.y, 0.0f, 1.0f);
            return {transformed.x, transformed.y};
        }

        math::Vector2 ToScreenPoint(const math::Vector2& pixelPoint, const i32 screenHeight)
        {
            return {pixelPoint.x, static_cast<f32>(screenHeight) - pixelPoint.y};
        }

        math::Vector2 GetHandlePixelPoint(const TransformationControlRectHandle& handle, const RectTransformControlBounds& bounds)
        {
            if (handle.Direction.x < 0.0f && handle.Direction.y > 0.0f) return bounds.TopLeft;
            if (handle.Direction.x == 0.0f && handle.Direction.y > 0.0f) return math::Vector2::Average(bounds.TopLeft, bounds.TopRight);
            if (handle.Direction.x > 0.0f && handle.Direction.y > 0.0f) return bounds.TopRight;
            if (handle.Direction.x < 0.0f && handle.Direction.y == 0.0f) return math::Vector2::Average(bounds.BottomLeft, bounds.TopLeft);
            if (handle.Direction.x > 0.0f && handle.Direction.y == 0.0f) return math::Vector2::Average(bounds.BottomRight, bounds.TopRight);
            if (handle.Direction.x < 0.0f && handle.Direction.y < 0.0f) return bounds.BottomLeft;
            if (handle.Direction.x == 0.0f && handle.Direction.y < 0.0f) return math::Vector2::Average(bounds.BottomLeft, bounds.BottomRight);
            return bounds.BottomRight;
        }

        math::Vector2 GetHandlePixelDirection(const TransformationControlRectHandle& handle, const RectTransformControlBounds& bounds)
        {
            if (handle.IsCorner) return math::Vector2::Right();
            if (handle.Direction.y > 0.0f) return bounds.TopRight - bounds.TopLeft;
            if (handle.Direction.y < 0.0f) return bounds.BottomRight - bounds.BottomLeft;
            if (handle.Direction.x < 0.0f) return bounds.TopLeft - bounds.BottomLeft;
            return bounds.TopRight - bounds.BottomRight;
        }

        math::Vector3 GetWorldPointOnControlPlane(const render::Camera& camera, const math::Vector2& screenPoint, const math::Plane& controlPlane)
        {
            const auto ray = camera.GetScreenPointToRay(screenPoint.x, screenPoint.y);
            math::Vector3 result;
            if (PlaneRayIntersection(controlPlane, ray, result)) return result;
            return ray.Origin + ray.Direction * 10.0f;
        }
    }

    UpdateTransformationControlsTransformsSystem::UpdateTransformationControlsTransformsSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void UpdateTransformationControlsTransformsSystem::UpdateMovementArrow(const TransformationControl& control, const TransformationControlMovementArrow& arrow, const math::Vector3& targetPosition, const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(arrow.Entity, Transform);
        t.SetWorldPosition(targetPosition);

        if (control.IsUsingWorldSpace())
        {
            t.SetWorldRotation(LookAt(arrow.Direction, math::Vector3::Up()));
        }
        else
        {
            t.SetWorldRotation(LookAt(arrow.Direction.Rotate(targetRotation), math::Vector3::Up()));
        }

        t.SetWorldScale(math::Vector3(controlScale, controlScale, controlScale));
    }

    void UpdateTransformationControlsTransformsSystem::UpdateMovementPlane(const TransformationControl& control, const TransformationControlMovementPlane& plane, const math::Vector3& targetPosition, const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(plane.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.06f : 1.0f;

        auto firstDirection = plane.FirstDirection;
        auto secondDirection = plane.SecondDirection;
        if (!control.IsUsingWorldSpace())
        {
            firstDirection = firstDirection.Rotate(targetRotation);
            secondDirection = secondDirection.Rotate(targetRotation);
        }

        const auto normal = math::Vector3::Normalize(math::Vector3::Cross(firstDirection, secondDirection));
        auto& t = GET(plane.Entity, Transform);
        t.SetWorldPosition(targetPosition);
        t.SetWorldRotation(LookAt(normal, secondDirection));
        t.SetWorldScale(math::Vector3(controlScale, controlScale, controlScale));
    }

    void UpdateTransformationControlsTransformsSystem::UpdateScaleArrow(const TransformationControl& control, const TransformationControlScaleArrow& arrow, const math::Vector3& targetPosition, const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(arrow.Entity, Transform);
        t.SetWorldPosition(targetPosition);

        if (control.IsUsingWorldSpace())
        {
            t.SetWorldRotation(LookAt(arrow.Direction, math::Vector3::Up()));
        }
        else
        {
            t.SetWorldRotation(LookAt(arrow.Direction.Rotate(targetRotation), math::Vector3::Up()));
        }

        t.SetWorldScale({controlScale, controlScale, controlScale * (1 + arrow.CurrentScaleMlt)});
    }

    void UpdateTransformationControlsTransformsSystem::UpdateScaleRoot(const TransformationControl& control, const TransformationControlScaleArrow& root, const math::Vector3& targetPosition, const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(control.RootScale.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= (isPointerInside ? 1.01f : 1) * 0.3f;

        auto& t = GET(root.Entity, Transform);
        t.SetWorldPosition(targetPosition);
        t.SetWorldRotation(control.IsUsingWorldSpace() ? glm::quat(1, 0, 0, 0) : targetRotation);
        t.SetWorldScale(math::Vector3(controlScale, controlScale, controlScale));
    }

    void UpdateTransformationControlsTransformsSystem::UpdateRotationRing(const TransformationControl& control, const TransformationControlRotationRing& ring, const math::Vector3& targetPosition, const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(ring.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(ring.Entity, Transform);
        t.SetWorldPosition(targetPosition);

        auto axisDirection = ring.Direction;
        if (!control.IsUsingWorldSpace())
        {
            axisDirection = axisDirection.Rotate(targetRotation);
        }

        t.SetWorldRotation(LookAt(axisDirection, math::Vector3::Up()));
        t.SetWorldScale(math::Vector3(controlScale, controlScale, controlScale));
    }

    bool UpdateTransformationControlsTransformsSystem::TryBuildRectTransformControlBounds(const TransformationControl& control, const render::Camera& camera, RectTransformControlBounds& bounds) const
    {
        i32 screenWidth = 1;
        i32 screenHeight = 1;
        camera.GetOutputSize(screenWidth, screenHeight);

        bool hasPoint = false;
        u32 validTargetsCount = 0;
        math::Vector2 aggregateMin = {};
        math::Vector2 aggregateMax = {};
        std::array<math::Vector2, 4> lastTargetCorners = {};

        for (const auto entity : control.TargetEntities)
        {
            if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform) || !HAS(entity, Transform)) continue;

            const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
            if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) continue;

            const auto& canvas = GET(canvasEntity, ui::Canvas);
            const auto logicalRect = ui_utility::CalculateRect(entity, canvasEntity, screenWidth, screenHeight);
            const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, screenWidth, screenHeight);
            const math::Rect pixelRect = {
                logicalRect.Min * scaleFactor,
                logicalRect.Max * scaleFactor
            };

            const auto pixelSize = pixelRect.GetSize();
            if (pixelSize.x <= 0.0f || pixelSize.y <= 0.0f) continue;

            const auto model = ui_utility::BuildModelMatrix(pixelRect, GET(entity, ui::RectTransform), GET(entity, Transform));
            const std::array<math::Vector2, 4> targetCorners = {
                TransformUiPixelPoint(model, {-0.5f, -0.5f}),
                TransformUiPixelPoint(model, {0.5f, -0.5f}),
                TransformUiPixelPoint(model, {0.5f, 0.5f}),
                TransformUiPixelPoint(model, {-0.5f, 0.5f})
            };

            lastTargetCorners = targetCorners;
            validTargetsCount++;

            for (const auto& point : targetCorners)
            {
                if (!hasPoint)
                {
                    aggregateMin = point;
                    aggregateMax = point;
                    hasPoint = true;
                    continue;
                }

                aggregateMin.x = (std::min)(aggregateMin.x, point.x);
                aggregateMin.y = (std::min)(aggregateMin.y, point.y);
                aggregateMax.x = (std::max)(aggregateMax.x, point.x);
                aggregateMax.y = (std::max)(aggregateMax.y, point.y);
            }
        }

        if (!hasPoint) return false;

        if (validTargetsCount == 1)
        {
            bounds.BottomLeft = lastTargetCorners[0];
            bounds.BottomRight = lastTargetCorners[1];
            bounds.TopRight = lastTargetCorners[2];
            bounds.TopLeft = lastTargetCorners[3];
        }
        else
        {
            bounds.BottomLeft = aggregateMin;
            bounds.BottomRight = {aggregateMax.x, aggregateMin.y};
            bounds.TopRight = aggregateMax;
            bounds.TopLeft = {aggregateMin.x, aggregateMax.y};
        }

        bounds.ScreenHeight = screenHeight;
        return true;
    }

    void UpdateTransformationControlsTransformsSystem::UpdateRectTransformHandle(const TransformationControlRectHandle& handle, const RectTransformControlBounds& bounds, const render::Camera& camera, const math::Plane& controlPlane, const glm::quat& cameraRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(handle.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.08f : 1.0f;

        const auto pixelPoint = GetHandlePixelPoint(handle, bounds);
        const auto screenPoint = ToScreenPoint(pixelPoint, bounds.ScreenHeight);
        const auto position = GetWorldPointOnControlPlane(camera, screenPoint, controlPlane);

        const auto baseSize = controlScale * 0.18f;
        auto rotation = cameraRotation;
        math::Vector3 scale(baseSize, baseSize, baseSize);

        if (!handle.IsCorner)
        {
            const auto pixelDirection = GetHandlePixelDirection(handle, bounds);
            const f32 angle = atan2(pixelDirection.y, pixelDirection.x);
            rotation = cameraRotation * glm::angleAxis(angle, glm::vec3(0, 0, 1));
            scale = math::Vector3(baseSize * 2.4f, baseSize * 0.75f, baseSize * 0.75f);
        }

        auto& transform = GET(handle.Entity, Transform);
        transform.SetWorldPosition(position);
        transform.SetWorldRotation(rotation);
        transform.SetWorldScale(scale);
    }

    void UpdateTransformationControlsTransformsSystem::UpdateRectTransformHandlePlaceholder(const TransformationControlRectHandle& handle, const math::Vector3& targetPosition, const glm::quat& cameraRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(handle.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.08f : 1.0f;

        const auto cameraRight = math::Vector3(cameraRotation * glm::vec3(1, 0, 0));
        const auto cameraUp = math::Vector3(cameraRotation * glm::vec3(0, 1, 0));
        const auto placeholderOffset = controlScale * 1.4f;
        const auto offset = cameraRight * (handle.Direction.x * placeholderOffset) + cameraUp * (handle.Direction.y * placeholderOffset);

        const auto baseSize = controlScale * 0.18f;
        const bool isHorizontalEdge = !handle.IsCorner && handle.Direction.y != 0.0f;
        const bool isVerticalEdge = !handle.IsCorner && handle.Direction.x != 0.0f;

        math::Vector3 scale(baseSize, baseSize, baseSize);
        if (isHorizontalEdge) scale = math::Vector3(baseSize * 2.4f, baseSize * 0.75f, baseSize * 0.75f);
        if (isVerticalEdge) scale = math::Vector3(baseSize * 0.75f, baseSize * 2.4f, baseSize * 0.75f);

        auto& transform = GET(handle.Entity, Transform);
        transform.SetWorldPosition(targetPosition + offset);
        transform.SetWorldRotation(cameraRotation);
        transform.SetWorldScale(scale);
    }

    void UpdateTransformationControlsTransformsSystem::OnUpdate()
    {
        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        const auto& control = GET(controlEntity, TransformationControl);
        if (!control.HasTargets()) return;

        const auto targetPosition = control.PivotWorldPosition;
        auto& primaryTargetTransform = GET(control.PrimaryTargetEntity, Transform);
        const auto targetRotation = control.HasRectTransformTargets
            ? mainCamera.Get().GetTransform().GetWorldRotation()
            : primaryTargetTransform.GetWorldRotation();

        const f32 controlScale = mainCamera.Get().CalculateConstantScale(targetPosition, 0.5f);

        UpdateMovementArrow(control, control.RightMovementArrow, targetPosition, targetRotation, controlScale);
        UpdateMovementArrow(control, control.UpMovementArrow, targetPosition, targetRotation, controlScale);
        UpdateMovementArrow(control, control.ForwardMovementArrow, targetPosition, targetRotation, controlScale);
        UpdateMovementPlane(control, control.RightUpMovementPlane, targetPosition, targetRotation, controlScale);
        UpdateMovementPlane(control, control.RightForwardMovementPlane, targetPosition, targetRotation, controlScale);
        UpdateMovementPlane(control, control.UpForwardMovementPlane, targetPosition, targetRotation, controlScale);

        UpdateScaleArrow(control, control.RightScaleArrow, targetPosition, targetRotation, controlScale);
        UpdateScaleArrow(control, control.UpScaleArrow, targetPosition, targetRotation, controlScale);
        UpdateScaleArrow(control, control.ForwardScaleArrow, targetPosition, targetRotation, controlScale);

        UpdateScaleRoot(control, control.RootScale, targetPosition, targetRotation, controlScale);

        UpdateRotationRing(control, control.RightRotationRing, targetPosition, targetRotation, controlScale);
        UpdateRotationRing(control, control.UpRotationRing, targetPosition, targetRotation, controlScale);
        UpdateRotationRing(control, control.ForwardRotationRing, targetPosition, targetRotation, controlScale);

        const auto& camera = mainCamera.Get();
        const auto cameraRotation = camera.GetTransform().GetWorldRotation();
        RectTransformControlBounds rectBounds;
        if (control.HasRectTransformTargets && TryBuildRectTransformControlBounds(control, camera, rectBounds))
        {
            const auto cameraForward = math::Vector3(cameraRotation * glm::vec3(0, 0, 1));
            const math::Plane controlPlane(cameraForward, targetPosition);
            UpdateRectTransformHandle(control.TopLeftRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.TopRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.TopRightRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.LeftRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.RightRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.BottomLeftRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.BottomRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            UpdateRectTransformHandle(control.BottomRightRectHandle, rectBounds, camera, controlPlane, cameraRotation, controlScale);
            return;
        }

        UpdateRectTransformHandlePlaceholder(control.TopLeftRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.TopRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.TopRightRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.LeftRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.RightRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.BottomLeftRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.BottomRectHandle, targetPosition, cameraRotation, controlScale);
        UpdateRectTransformHandlePlaceholder(control.BottomRightRectHandle, targetPosition, cameraRotation, controlScale);
    }
}
