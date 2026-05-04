#include "pch.h"
#include "UpdateTransformationControlsTransformsSystem.h"

#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
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
    }
}
