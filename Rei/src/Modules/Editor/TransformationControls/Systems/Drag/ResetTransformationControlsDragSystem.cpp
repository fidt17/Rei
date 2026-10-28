#include "pch.h"
#include "ResetTransformationControlsDragSystem.h"

#include "Modules/Input/Input.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    ResetTransformationControlsDragSystem::ResetTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld, const std::shared_ptr<EditorActionStore>& actions) :
        System(ecsWorld), _actions(actions)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void ResetTransformationControlsDragSystem::OnUpdate()
    {
        if (Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        RecordMove(control);
        ResetDragState(control);
    }

    void ResetTransformationControlsDragSystem::RecordMove(const TransformationControl& control) const
    {
        if (control.Mode != Movement || control.DragStartTargetStates.size() != 1) return;
        const bool dragging = control.RightMovementArrow.DragActive || control.UpMovementArrow.DragActive || control.ForwardMovementArrow.DragActive
            || control.RightUpMovementPlane.DragActive || control.RightForwardMovementPlane.DragActive || control.UpForwardMovementPlane.DragActive;
        if (!dragging) return;

        const auto& start = control.DragStartTargetStates.front();
        if (IS_DEAD(start.Entity) || !HAS(start.Entity, Transform) || HAS(start.Entity, ui::RectTransform)) return;
        const ecs::ComponentRef<Transform> transform(_ecs, start.Entity);
        const auto before = start.LocalPosition;
        const auto after = transform.Get().GetLocalPosition();
        if (before.x == after.x && before.y == after.y && before.z == after.z) return;
        _actions->RecordApplied(std::make_unique<EntityMoveAction>(transform, before, after));
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
