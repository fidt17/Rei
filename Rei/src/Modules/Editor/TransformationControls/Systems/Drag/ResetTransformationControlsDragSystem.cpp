#include "pch.h"
#include "ResetTransformationControlsDragSystem.h"

#include "Modules/Input/Input.h"

namespace rei::editor
{
    ResetTransformationControlsDragSystem::ResetTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void ResetTransformationControlsDragSystem::OnUpdate()
    {
        if (Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        ResetDragState(GET(controlEntity, TransformationControl));
    }

    void ResetTransformationControlsDragSystem::ResetDragState(TransformationControl& control) const
    {
        control.RightMovementArrow.DragActive = false;
        control.UpMovementArrow.DragActive = false;
        control.ForwardMovementArrow.DragActive = false;
        control.RightUpMovementPlane.DragActive = false;
        control.RightForwardMovementPlane.DragActive = false;
        control.UpForwardMovementPlane.DragActive = false;

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

        control.TopLeftRectHandle.DragActive = false;
        control.TopRectHandle.DragActive = false;
        control.TopRightRectHandle.DragActive = false;
        control.LeftRectHandle.DragActive = false;
        control.RightRectHandle.DragActive = false;
        control.BottomLeftRectHandle.DragActive = false;
        control.BottomRectHandle.DragActive = false;
        control.BottomRightRectHandle.DragActive = false;

        control.TopLeftRectHandle.DragStartPosition = {};
        control.TopRectHandle.DragStartPosition = {};
        control.TopRightRectHandle.DragStartPosition = {};
        control.LeftRectHandle.DragStartPosition = {};
        control.RightRectHandle.DragStartPosition = {};
        control.BottomLeftRectHandle.DragStartPosition = {};
        control.BottomRectHandle.DragStartPosition = {};
        control.BottomRightRectHandle.DragStartPosition = {};

        control.RectTransformBodyDragPending = false;
        control.RectTransformBodyDragActive = false;
        control.RectTransformBodyDragStartPosition = {};

        control.DragStartTargetStates.clear();
    }
}
