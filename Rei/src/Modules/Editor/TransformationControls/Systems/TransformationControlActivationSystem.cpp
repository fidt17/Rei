#include "pch.h"
#include "TransformationControlActivationSystem.h"

#include "Modules/Components/ActiveTag.h"
#include "Modules/Input/Input.h"

namespace rei::editor
{
    TransformationControlActivationSystem::TransformationControlActivationSystem(const std::shared_ptr<ecs::World>& ecsWorld): System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void TransformationControlActivationSystem::DisableMovementControls(const TransformationControl& transformationControl) const
    {
        DISABLE(transformationControl.RightMovementArrow.Entity)
        DISABLE(transformationControl.UpMovementArrow.Entity)
        DISABLE(transformationControl.ForwardMovementArrow.Entity)
        DISABLE(transformationControl.RightUpMovementPlane.Entity)
        DISABLE(transformationControl.RightForwardMovementPlane.Entity)
        DISABLE(transformationControl.UpForwardMovementPlane.Entity)
    }

    void TransformationControlActivationSystem::DisableScaleControls(const TransformationControl& transformationControl) const
    {
        DISABLE(transformationControl.RightScaleArrow.Entity)
        DISABLE(transformationControl.UpScaleArrow.Entity)
        DISABLE(transformationControl.ForwardScaleArrow.Entity)
        DISABLE(transformationControl.RootScale.Entity)
    }

    void TransformationControlActivationSystem::DisableRotationControls(const TransformationControl& transformationControl) const
    {
        DISABLE(transformationControl.RightRotationRing.Entity)
        DISABLE(transformationControl.UpRotationRing.Entity)
        DISABLE(transformationControl.ForwardRotationRing.Entity)
    }

    void TransformationControlActivationSystem::EnableMovementControls(const TransformationControl& transformationControl) const
    {
        ENABLE(transformationControl.RightMovementArrow.Entity)
        ENABLE(transformationControl.UpMovementArrow.Entity)
        ENABLE(transformationControl.ForwardMovementArrow.Entity)
        ENABLE(transformationControl.RightUpMovementPlane.Entity)
        ENABLE(transformationControl.RightForwardMovementPlane.Entity)
        ENABLE(transformationControl.UpForwardMovementPlane.Entity)
    }

    void TransformationControlActivationSystem::EnableScaleControls(const TransformationControl& transformationControl) const
    {
        ENABLE(transformationControl.RightScaleArrow.Entity)
        ENABLE(transformationControl.UpScaleArrow.Entity)
        ENABLE(transformationControl.ForwardScaleArrow.Entity)
        ENABLE(transformationControl.RootScale.Entity)
    }

    void TransformationControlActivationSystem::EnableRotationControls(const TransformationControl& transformationControl) const
    {
        ENABLE(transformationControl.RightRotationRing.Entity)
        ENABLE(transformationControl.UpRotationRing.Entity)
        ENABLE(transformationControl.ForwardRotationRing.Entity)
    }

    void TransformationControlActivationSystem::OnUpdate()
    {
        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& transformationControl = GET(controlEntity, TransformationControl);

        if (!transformationControl.HasTargets())
        {
            DisableMovementControls(transformationControl);
            DisableScaleControls(transformationControl);
            DisableRotationControls(transformationControl);
            return;
        }

        if (Input::IsKeyPressed(GLFW_KEY_W))
        {
            transformationControl.Mode = Movement;
        }
        else if (Input::IsKeyDown(GLFW_KEY_E))
        {
            transformationControl.Mode = Scale;
        }
        else if (Input::IsKeyDown(GLFW_KEY_R))
        {
            transformationControl.Mode = Rotation;
        }
        
        if (transformationControl.Mode == Movement)
        {
            EnableMovementControls(transformationControl);
            DisableScaleControls(transformationControl);
            DisableRotationControls(transformationControl);
        }
        else if (transformationControl.Mode == Scale)
        {
            DisableMovementControls(transformationControl);
            EnableScaleControls(transformationControl);
            DisableRotationControls(transformationControl);
        }
        else if (transformationControl.Mode == Rotation)
        {
            DisableMovementControls(transformationControl);
            DisableScaleControls(transformationControl);
            EnableRotationControls(transformationControl);
        }
    }
}
