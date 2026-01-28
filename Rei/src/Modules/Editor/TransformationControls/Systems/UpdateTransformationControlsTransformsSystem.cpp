#include "pch.h"
#include "UpdateTransformationControlsTransformsSystem.h"

#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "assimp/port/AndroidJNI/AndroidJNIIOSystem.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    UpdateTransformationControlsTransformsSystem::UpdateTransformationControlsTransformsSystem(
        const std::shared_ptr<ecs::World>& ecsWorld): System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void UpdateTransformationControlsTransformsSystem::UpdateMovementArrow(const TransformationControl& control,
                                                                           const TransformationControlMovementArrow& arrow, const math::Vector3& targetPosition,
                                                                           const glm::quat& targetRotation, f32 controlScale) const
    {
        const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(arrow.Entity, Transform);
        t.GetPosition() = targetPosition;

        if (control.UseWorldSpace)
        {
            t.SetRotation(LookAt(arrow.Direction, math::Vector3::Up()));
        }
        else
        {
            t.SetRotation(LookAt(arrow.Direction.Rotate(targetRotation), math::Vector3::Up()));
        }

        t.GetScale() = math::Vector3(controlScale, controlScale, controlScale);
    }

    void UpdateTransformationControlsTransformsSystem::UpdateScaleArrow(const TransformationControl& control, const TransformationControlScaleArrow& arrow,
                                                                        const math::Vector3& targetPosition, const glm::quat& targetRotation,
                                                                        f32 controlScale) const
    {
        const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(arrow.Entity, Transform);
        t.GetPosition() = targetPosition;
        t.SetRotation(LookAt(arrow.Direction.Rotate(targetRotation), math::Vector3::Up()));
        t.GetScale() = {controlScale, controlScale, controlScale * (1 + arrow.CurrentScaleMlt)};
    }

    void UpdateTransformationControlsTransformsSystem::UpdateScaleRoot(const TransformationControl& control, const TransformationControlScaleArrow& root,
                                                                       const math::Vector3& targetPosition, const glm::quat& targetRotation,
                                                                       f32 controlScale) const
    {
        const auto isPointerInside = GET(control.RootScale.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= (isPointerInside ? 1.01f : 1) * 0.3f;

        auto& t = GET(root.Entity, Transform);
        t.GetPosition() = targetPosition;

        t.SetRotation(targetRotation);

        t.GetScale() = math::Vector3(controlScale, controlScale, controlScale);
    }

    void UpdateTransformationControlsTransformsSystem::UpdateRotationRing(const TransformationControl& control, const TransformationControlRotationRing& ring,
                                                                          const math::Vector3& targetPosition, const glm::quat& targetRotation,
                                                                          f32 controlScale) const
    {
        const auto isPointerInside = GET(ring.Entity, physics::PointerCollisionListener).IsInside;
        controlScale *= isPointerInside ? 1.01f : 1;

        auto& t = GET(ring.Entity, Transform);
        t.GetPosition() = targetPosition;

        auto axisDirection = ring.Direction;
        if (!control.UseWorldSpace)
        {
            axisDirection = axisDirection.Rotate(targetRotation);
        }

        t.SetRotation(LookAt(axisDirection, math::Vector3::Up()));
        t.GetScale() = math::Vector3(controlScale, controlScale, controlScale);
    }

    void UpdateTransformationControlsTransformsSystem::OnUpdate()
    {
        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;
        const auto& control = GET(controlEntity, TransformationControl);

        if (IS_DEAD(control.TargetEntity)) return;

        auto& targetTransform = GET(control.TargetEntity, Transform);
        const auto& targetPosition = targetTransform.GetPosition();
        const auto& targetRotation = targetTransform.GetRotation();

        const f32 controlScale = mainCamera.Get().CalculateConstantScale(targetPosition, 0.5);

        UpdateMovementArrow(control, control.RightMovementArrow, targetPosition, targetRotation, controlScale);
        UpdateMovementArrow(control, control.UpMovementArrow, targetPosition, targetRotation, controlScale);
        UpdateMovementArrow(control, control.ForwardMovementArrow, targetPosition, targetRotation, controlScale);

        UpdateScaleArrow(control, control.RightScaleArrow, targetPosition, targetRotation, controlScale);
        UpdateScaleArrow(control, control.UpScaleArrow, targetPosition, targetRotation, controlScale);
        UpdateScaleArrow(control, control.ForwardScaleArrow, targetPosition, targetRotation, controlScale);

        UpdateScaleRoot(control, control.RootScale, targetPosition, targetRotation, controlScale);

        UpdateRotationRing(control, control.RightRotationRing, targetPosition, targetRotation, controlScale);
        UpdateRotationRing(control, control.UpRotationRing, targetPosition, targetRotation, controlScale);
        UpdateRotationRing(control, control.ForwardRotationRing, targetPosition, targetRotation, controlScale);
    }
}
